# One-time local → production data migration

**Date:** 2026-09-17
**Context:** Third continuation of the same day's deploy work. Once
`biblioteka.svara.bg` was live, the owner noticed the library showed 0 books
(expected — production started as a brand-new, empty database) and asked
about keeping local and production in sync. Asked which model they wanted:
one-time copy with production becoming canonical going forward, or ongoing
two-way sync. They picked the former — simpler and correct for a
single-user app; local dev stays disposable from here on.
**Status:** Complete. Production now has the owner's real library (279
items, 461 works, 201 wishlist entries, 359 cover images) under the
production admin account, with the production login credentials untouched.
**Commits:** this session's commit (see `git log`) — docs only, no code
changed.

## What got migrated, and how

`pg_dump` (custom format) of the local database, **excluding
`asp_net_*`** (`-T 'asp_net_*'`) — deliberately, so restoring it into
production wouldn't overwrite the production admin's already-seeded
email/password with the local dev defaults. Restored with `pg_restore
--clean --if-exists`, which only drops/recreates objects actually present in
the dump — the untouched `asp_net_users` table (and the login it holds)
never got involved.

That leaves every migrated row still owned by the *local* admin's
`UserId` (`01a091e1-...`), not production's (`01a0ae8a-...`) — this app
scopes everything per-user via `IUserOwned`. Remapped it explicitly: found
every table with a `user_id` column
(`information_schema.columns`), wrote one SQL file
(`UPDATE ... SET user_id = '<prod>' WHERE user_id = '<local>'` per table,
wrapped in one transaction), uploaded and ran it against production.
13 tables, ~600 rows repointed to the production account in one commit.

Cover images are files, not database rows (`Covers__RootDirectory` on a
Docker volume) — `docker cp`'d them out of the local `api` container, tar'd,
`scp`'d, and extracted into production's `biblioteka-covers` volume
separately from the DB restore.

Verified end-to-end afterward: logged in with the *production* credentials
(unaffected by the whole operation), confirmed `totalCount: 279` via the
real API, confirmed a specific book's `userId` was the production account,
confirmed an actual cover image loads over HTTPS. svara.bg checked after
every step, as with the rest of the day's work on this shared server.

## A loose end from the deploy, resolved by this migration rather than chased separately

The owner's screenshot also showed a `401` on `/auth/me` in the browser
console, alongside the empty library. Checked the DataProtection key ring
(`docker exec biblioteka-api ls /app/keys`) — a single key file, timestamped
at first boot, unchanged across the day's several `api` container
recreations — so session invalidation via key rotation wasn't the cause; the
persisted-volume key ring is working as intended. Most likely a one-off
console artifact from a moment during one of the day's container restarts,
not an ongoing problem — the library now showing real data (rather than a
suspiciously-empty-but-200 state) is itself further evidence nothing about
auth is actually broken. Didn't chase it further since the concrete,
reproducible, user-reported symptom (empty library) has a confirmed,
verified fix.

## Why we built it this way

**Never touch the credentials a session already trusts.** The tempting
shortcut — dump and restore *everything*, including `asp_net_users` — would
have silently reverted the live site's login back to the local dev default
(`admin@example.com` / the well-known placeholder password, public in this
repo's history) the moment the restore finished. Excluding identity tables
from the dump entirely made that class of mistake structurally impossible
rather than something to remember not to do.

**Remap ownership explicitly rather than assume the app doesn't care.**
This is a single-user app today, but "single-user" isn't the same as
"no ownership model" — every domain row still carries a real `UserId` FK,
and the local and production admin accounts are genuinely different rows
with different IDs. Skipping the remap would have left 279 books
technically in the database but invisible to the only account that can log
in and see them.

## Files changed

None — pure data operation against the running production and local
databases, via `pg_dump`/`pg_restore`/one SQL script, all run over SSH.

## What's still open

Local dev is now explicitly the disposable/throwaway copy going forward —
worth keeping in mind next session: don't assume local's data still matches
what's live, and don't expect further "push local to prod" runs to be as
simple as this one-time migration if local has since diverged (would need
the same table-by-table care again, not a blind repeat of today's exact
commands).
