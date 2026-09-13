# Postgres backup/restore scheme for the Docker-only stack

**Date:** 2026-09-13
**Context:** The project currently only runs in Docker (no managed hosting,
no separate backup infrastructure) — a `docker compose down -v`, a corrupted
`pgdata` volume, or new hardware would wipe the owner's real library
(currently 199 works / 198 editions) with no way back. Owner asked for a
scheduled backup — weekly, or on any book add/edit/delete — written to a
host file in the repo folder but **not** git-tracked, plus a `docker-compose`
mechanism that restores from it automatically if present, or starts empty
if not.
**Status:** Complete — built and verified end-to-end against the real
running stack and a throwaway restore target, not just read for plausibility.
**Commits:** landed in a single commit this session (see `git log`).

## What we built

### 1. `db-backup` sidecar service (`docker-compose.yml`, `docker/db-backup/backup.sh`)

A `postgres:17-alpine` container (has `pg_dump`/`psql` matching the server
version) added to `docker-compose.yml`, `depends_on: db (healthy)`. On start
it:

- Installs (idempotently — `CREATE OR REPLACE`/`DROP TRIGGER IF EXISTS`) a
  `pg_notify('book_changed', TG_TABLE_NAME)` trigger, `FOR EACH STATEMENT`,
  on `works` and `editions` — the two tables a book add/edit/delete actually
  touches. Retries in a loop if the tables don't exist yet (fresh DB, `api`
  hasn't applied EF Core migrations yet) instead of crashing.
- Polls for that notification with the classic psql idiom —
  `psql -c "LISTEN book_changed;" -c "SELECT pg_sleep(30);"` and grep the
  output for "Asynchronous notification" — since plain `psql` only surfaces
  `LISTEN` traffic when a query completes, not while idle.
- On a notification, runs `pg_dump --format=custom` to
  `db-backup/mydigitallibrary.dump` (atomically — dump to `.tmp`, then
  `mv`), **debounced to at most once every 5 minutes** so a bulk CSV import
  (hundreds of row-level statements) collapses into one backup instead of
  hundreds.
- Independently, backs up unconditionally if the existing dump is more than
  7 days old (weekly ceiling), even with zero changes.

### 2. Restore-on-fresh-volume (`docker/db-init/10-restore-if-exists.sh`)

Mounted into `db`'s `/docker-entrypoint-initdb.d/` — the official Postgres
image only runs scripts there on a **fresh, empty** data directory, which is
exactly the "container/volume got destroyed" scenario and never fires
against a volume that already has data. If `db-backup/mydigitallibrary.dump`
exists, `pg_restore --no-owner --clean --if-exists` runs it before the
server accepts connections; if not, Postgres just starts with an empty
database as before. No app code or migration changes needed.

### 3. Not git-tracked

`db-backup/*` added to `.gitignore` with `!db-backup/README.md` carved out,
so the dump itself never gets committed but the folder still explains
itself on a fresh clone.

## Why we built it this way

**Trigger + `LISTEN`/`NOTIFY` instead of hooking the API's book
endpoints.** Firing the backup from inside `WorkService`/`EditionService`
would work too, but only for changes that went through the API — a direct
`psql` fix, a future admin script, or a migration wouldn't trigger it. A DB
trigger fires no matter how the row changed, and keeps the backup mechanism
fully decoupled from application code (nothing in `src/` had to change).

**`FOR EACH STATEMENT`, not `FOR EACH ROW`.** A `NOTIFY` per row would mean
one per book in a CSV import; per-statement means one per `INSERT`/`UPDATE`/
`DELETE` command regardless of how many rows it touches — already coarser
before the 5-minute debounce even kicks in.

**Debounce on the notify path, no debounce on the weekly path.** The two
have different jobs: change-triggered backups should happen soon-ish but
not thrash the disk during a bulk import; the weekly fallback is a hard
ceiling on staleness and must fire regardless of recent activity — gating
it on the same 5-minute debounce would let heavy day-to-day use silently
suppress the guarantee.

**Restore lives in the official `docker-entrypoint-initdb.d` hook, not a
custom check-and-restore script run every startup.** Piggy-backing on
Postgres's own "only on a truly fresh data directory" semantics means there
is no risk of ever overwriting live data on a normal restart — the
mechanism is inert unless the volume is actually gone.

## Verified live (not just read for plausibility)

- Started `db-backup` against the real stack: it installed both triggers,
  immediately took a first backup (no prior file), and the dump
  (`pg_restore --list`) contains data for **28 tables**.
- Ran `UPDATE works SET title = title WHERE id = ...` (a no-op write,
  reverting nothing since it changes no data) against the live `works`
  table — confirmed in the sidecar's logs that the trigger fired, `NOTIFY`
  was received, and the debounce correctly logged "backed up Ns ago -
  skipping" since a backup had just run.
- Full disaster-recovery round trip, deliberately **not** against the
  real `pgdata` named volume: created a scratch Docker volume + a throwaway
  `postgres:17-alpine` container with the same bind mounts, confirmed
  `10-restore-if-exists.sh` ran on the fresh volume, and that the restored
  database had the exact same row counts as the live one (199 works / 198
  editions) with matching titles — then deleted the scratch container and
  volume.
- Re-checked the live `works`/`editions` counts one final time after all
  testing (still 199/198, untouched) before writing this history entry.

## Files created/changed

- `docker-compose.yml` — new `db-backup` service; `db` gets the backup bind
  mount (read-only) and the `10-restore-if-exists.sh` init script mount
- `docker/db-backup/backup.sh` — new, the trigger-install + poll/backup loop
- `docker/db-init/10-restore-if-exists.sh` — new, restore-on-fresh-volume
- `db-backup/README.md` — new, tracked (the one exception to the ignore rule
  below); explains the folder and gives manual backup/restore commands
- `.gitignore` — `/db-backup/*` ignored, `!/db-backup/README.md` kept
- `README.md` — short section on the backup/restore behavior, in Bulgarian
  matching the rest of the file

## What's still open

- Single rolling dump file, no retention/rotation of older backups — matches
  what was asked for (a recovery snapshot, not a full backup history); worth
  revisiting only if the owner later wants point-in-time recovery.
- The dump only lives on the host filesystem next to the repo — no offsite
  copy. Fine for the current "just don't lose it to a bad `docker compose
  down -v`" goal, not a substitute for real offsite backup if that ever
  matters.
- `MIN_GAP_SECONDS` (5 min) and `WEEKLY_SECONDS` (7 days) are hardcoded in
  `backup.sh` rather than env-configurable — deliberately kept simple since
  there was no stated need to tune them per environment.

## Next

Nothing queued. The owner should let `docker compose up -d` run normally and
confirm the same `db-backup` logs appear in their own environment.
