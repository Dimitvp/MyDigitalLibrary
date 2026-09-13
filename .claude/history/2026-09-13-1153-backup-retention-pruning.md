# Backup retention: count-first pruning, verified against the running sidecar

**Date:** 2026-09-13
**Context:** Direct follow-up to the same-day backup overhaul
(`2026-09-13-1133-...` is unrelated; the actual prior entry is the
"backup missed non-works/editions changes" fix earlier today, commit
`819d1bf`) — that fix made every backup a separate, never-overwritten
timestamped file, which immediately raises "won't this grow forever?" The
owner asked for pruning with an explicit, carefully-specified rule: check
file **count** before file **age**, so a burst of old-but-still-wanted
backups can never all get deleted in one pass.
**Status:** Complete. Logic unit-tested in isolation against three
scenarios before touching the real container, then deployed and confirmed
running against the live `docker compose` stack.
**Commits:** this session's second backup-related commit (see `git log`).

## The rule, as specified

1. If there are **10 or fewer** backups total, delete nothing — no matter
   how old.
2. Otherwise, the newest 10 are always kept regardless of age. Only
   among the excess (older) files does age matter, and only those older
   than **~6 months** get deleted.

Count is checked first, strictly before age, specifically so the backup
directory can never be pruned down to nothing (or below the floor) in a
single pass — the owner's explicit concern ("да не в един момент затрием
всичко без да искаме").

## Implementation

Added `prune_backups()` to `docker/db-backup/backup.sh`, called once at
the end of every *successful* `run_backup()` (never on a failed dump, so
a failure can't cascade into deleting good backups):

```sh
prune_backups() {
    total=$(ls -1 "$BACKUP_DIR"/mydigitallibrary_*.dump 2>/dev/null | wc -l)
    if [ "$total" -le "$KEEP_MIN" ]; then
        return
    fi
    excess=$((total - KEEP_MIN))
    cutoff=$(( $(date +%s) - MAX_AGE_SECONDS ))
    n=0
    ls -1 "$BACKUP_DIR"/mydigitallibrary_*.dump | sort | while IFS= read -r f; do
        n=$((n + 1))
        if [ "$n" -le "$excess" ]; then
            file_epoch=$(stat -c %Y "$f")
            [ "$file_epoch" -lt "$cutoff" ] && { rm -f "$f"; log "prune: removed $f"; }
        fi
    done
}
```

(shown simplified — the real file uses full `if` blocks throughout, see
below for why). `KEEP_MIN=10`, `MAX_AGE_SECONDS=$((180*24*3600))` (~6
months) sit alongside the existing `POLL_SECONDS`/`MIN_GAP_SECONDS`/
`WEEKLY_SECONDS` constants at the top of the script.

## A `set -e` trap avoided, not hit

The script runs under `set -eu`. The natural way to write "stop once
we're past the excess region" is `[ "$n" -gt "$excess" ] && break` inside
the `while read` loop — but under `errexit`, that construct's exit status
is 1 on every iteration where the condition is *false* (which is most
iterations), and `set -e` would abort the script right there. Avoided by
dropping the early-`break` entirely: the loop now visits every file
unconditionally (harmless — personal-library backup counts are in the
tens, not thousands) and uses a plain `if "$n" -le "$excess"` guard
instead, which is exempt from errexit like any `if` condition.

## Verified

- **Isolated unit test** (temp directory, real `touch -d` timestamps, the
  actual pruning shell logic extracted verbatim) against three cases
  before touching the real backup folder:
  1. 8 files, all from January 2024 (over a year stale) — **0 deleted**,
     because `total (8) <= KEEP_MIN (10)`.
  2. 13 files (8 stale Jan-2024 + 5 fresh) — the 3 oldest (beyond the
     10-file floor) deleted, landing at exactly 10 remaining.
  3. 12 fresh files, none older than 6 months — **0 deleted**, even
     though 2 of them sit in the "excess" region past the 10-file floor
     (count alone doesn't trigger deletion; age still has to hold too).
- **Live deploy**: `docker compose restart db-backup` — script re-read
  from the bind mount, no image rebuild needed. Logs confirmed the old
  per-table trigger names were already gone (from the earlier fix, not a
  regression) and the new trigger set re-installed cleanly.
- Confirmed the backup directory currently holds only 3 files (2
  timestamped + 1 legacy `mydigitallibrary.dump` from before the previous
  fix) — nowhere near `KEEP_MIN`, so no live pruning has fired yet; the
  isolated test is what actually exercises the deletion path for now.

## Why we built it this way

**Test destructive logic against a throwaway directory before letting it
loose on the real backup folder.** Pruning's whole job is deleting files
from the one thing standing between the owner and data loss if the
database container ever dies badly — verifying it in isolation first
(with `touch -d`-faked ages covering all three branches of the rule) means
any bug would have shown up against disposable test files, not the actual
backup history.

**Count-before-age isn't just "check two conditions" — the order encodes
the actual invariant.** Checking age first and count second would let a
neglected database (no changes, no backups, for over 6 months, however
many are on disk) get pruned to zero once age alone is the trigger — count
gating first is what guarantees the floor holds unconditionally.

## Files changed

- `docker/db-backup/backup.sh` — `prune_backups()`, `KEEP_MIN`/`MAX_AGE_SECONDS`, called from `run_backup()`
- `db-backup/README.md` — replaced the "grows unbounded, no retention" note with the count-first pruning rule

## What's still open

Nothing queued — this closes out the backup-robustness follow-up from
earlier today. `db-backup/README.md`'s note about unbounded growth
(written earlier today, before this fix existed) was updated in the same
pass to describe the count-first pruning rule instead.

## Next

Nothing queued.
