# Session wrap-up: from a broken local Docker restart to a live production deploy

**Date range:** 2026-09-16 → 2026-09-17
**Purpose:** This conversation ran long and covered a lot of ground — a
consolidated wrap-up, in the spirit of svara-bg's own
`.claude/reports/2026-08-18-1809-hetzner-deploy-conversation-wrapup.md`
(referenced repeatedly today as a direct model for several of this session's
own decisions). Detailed, dated entries already exist for most of the
larger pieces of work; this ties them together, fills in the few things
that never got their own entry, and states what's true *right now* before a
fresh thread picks this up.
**Status:** MyDigitalLibrary is **live in production** at
`https://biblioteka.svara.bg`, populated with the owner's real library (279
books), alongside svara.bg on the same Hetzner box — checked healthy and
unaffected at every step along the way.

## The arc, in order

### 1. A Docker Desktop restart broke the site (2026-09-16 morning)
Diagnosed and fixed: `api` had no `restart` policy (unlike `db-backup`),
so it stayed `Exited` after the host restarted rather than the `db` DNS-
timing race that actually killed it the first time. Backups turned out
fine — the debounced `db-backup` sidecar is event-driven, not continuous,
so a ~17h gap since the last snapshot was expected, not a fault.
Full detail: `.claude/history/2026-09-16-0645-docker-restart-resilience-frontend-containerized.md`.

### 2. Frontend containerized, twice (same day)
First pass: a dev-mode `ng serve` container for hot-reload. The owner then
asked how the sibling `svara-bg` project solved the same problem, saw its
nginx-production-build pattern, and chose to match it — redone as a
multi-stage Dockerfile (`node` build → `nginx` runtime), `web` service in
`docker-compose.yml`, all three app services given `restart: unless-stopped`.
Same history entry as above covers this.

**A bug introduced by that first pass, found the next message:** the new
`nginx.conf`'s cache-control regex location (`~* \.(jpg|png|...)$`) silently
outranked the plain `location /covers/` proxy block — nginx's own matching
rules give regex locations priority over a bare prefix location — so every
cover image 404'd. Fixed by adding `^~` to force the prefix locations to win
outright (the exact same safeguard `svara-bg`'s own nginx config already had
for `/avatars/`, which should have been copied the first time). This fix
never got its own history file; recorded here for completeness.

### 3. Editable book format + a real duplicate-guard bug (2026-09-16)
The owner couldn't change "The Origins of Political Order" from Ebook to
Audiobook in the UI — `Format` was set-once at creation with no mutator on
either `Edition` or `LibraryItem`, unlike every other field. Added
`ChangeFormat` to both (clearing whatever the existing throwing setters
would now reject — ISBN/page count for Audiobook, cover type off Physical,
etc.), a `PATCH .../format` endpoint, and turned the read-only label into a
dropdown. Full detail: `.claude/history/2026-09-16-0930-editable-library-item-format.md`.

### 4. Libristo CSV reconciliation, twice, converging on a real product bug (2026-09-16)
Reconciled a Libristo wishlist export against the catalog. First pass
classified matches by title+language only; the owner corrected it hard —
duplicates must match on **format** too, not just title/language ("owning
the ebook doesn't mean I don't also want the physical copy"). That
correction turned out to describe a real bug in
`WishlistService.EnsureNotAlreadyOwnedAsync` itself (format-blind, would
have blocked the same thing through the real UI form, not just this
import), fixed and tested. A same-day follow-up resolved four ISBN/format
anomalies the fix surfaced (turned out to be Goodreads-import artifacts —
format was never reliably known for those, only *read/want-to-read* was)
and confirmed a Lawrence Krauss UK/US title-variant pair via a web search.
Full detail: `.claude/history/2026-09-16-1330-libristo-csv-reconcile-format-aware-dedup-fix.md`.

### 5. Two Audible library imports (2026-09-17)
**Finished audiobooks (40 rows):** converted 20 Goodreads-import items from
the now-known-wrong "Ebook" tag to "Audiobook" (using the `ChangeFormat`
work from #3), added 18 new owned Audiobook items, enriched 2 already-
correct ones — narrator, cover, `Audible рейтинг` note, and a same-day
`ReadingSession` (Finished) for all of them, since exact listen-completion
dates weren't in the export. Manually caught two matcher misses (short
titles "Propaganda"/"Nexus") the automated title-similarity script couldn't
see on its own. Full detail:
`.claude/history/2026-09-17-0900-audible-finished-library-import.md`.

**Unread/in-progress audiobooks (11 rows):** the owner's own message named
"The Stasi" twice with contradictory progress numbers — asked rather than
guessed, confirmed it was a copy-paste slip (the second mention's numbers
actually belonged to "Merlin's Tour of the Universe"). Marked one book
`Reading` (left open, no end date), one `Finished` despite Audible showing
95%, the rest not-started — including Merlin's Tour, despite Audible's
stray 9%. Full detail:
`.claude/history/2026-09-17-1000-audible-unread-library-import.md`.

### 6. Two small library-list filters (2026-09-17)
Neither got its own history file — recorded here:
- **"Без категория" filter option** — the category dropdown could group by
  "no genre" for display but had no way to *filter* down to just those
  items (54 of them, mostly from the day's bulk imports). Added a
  client-side sentinel value (the backend's `genreId` param has no concept
  of "absent"), reusing the same empty-genre check the grouping view
  already did. Also fixed the list's empty-state check to key off the
  post-filter `groups()` rather than the raw unfiltered API response, so an
  empty filtered result actually shows the "nothing here" message instead
  of silently rendering nothing.
- **Format filter (Physical/Ebook/Audiobook)** — the backend's `GET
  /library-items?format=` param already existed and needed no changes; just
  a third dropdown alongside reading-status and category, reusing the
  `library.format.*` labels already used elsewhere in the app.

### 7. Hetzner hosting cost + domain question (2026-09-17)
The owner asked what it would cost to add MyDigitalLibrary to the same
Hetzner box already running svara.bg, and whether they could reach it
without buying a new domain. Read svara-bg's own deploy reports to answer
from real numbers rather than guessing: CX33, 4 vCPU/8GB/80GB, ~€10.79/mo
already paid for svara.bg's own 11-container stack. Estimated
MyDigitalLibrary's footprint at well under 1GB RAM — the owner then checked
live and confirmed 5.2GiB RAM / 48GB disk free, comfortably enough headroom
to add this app to the *same* server at **no additional Hetzner cost**.
For the domain question: recommended a free subdomain of the already-owned
`svara.bg` (reusing the existing Hetzner DNS zone and nginx+certbot pattern)
over buying anything new. The owner picked `biblioteka.svara.bg`.

### 8. Pre-deploy security hardening (2026-09-17)
Before exposing an app that had only ever run on localhost, did a targeted
security pass (login/logout, cookie config, every endpoint's authorization,
production readiness) and found three real gaps, all fixed and tested:
no login lockout/rate-limiting (unlimited password guesses against the one
admin account), no `ForwardedHeadersMiddleware` (would have silently
dropped the cookie's `Secure` flag behind the planned nginx reverse proxy —
the exact bug class svara-bg's own deploy already hit once), and migrations
+ admin seeding gated to `IsDevelopment()` only (a `Production` boot would
have had no schema and no way to log in at all, ever). Full detail:
`.claude/history/2026-09-17-1400-pre-deploy-security-hardening.md`.

### 9. Deploy files written and locally verified (2026-09-17)
`docker-compose.prod.yml` (single hand-written file, mirroring svara-bg's
own — this exact host has a known container-networking bug when a compose
project is assembled from multiple `-f` files), a host nginx config for the
subdomain, `.env.production.example`, `deploy/README.md`. A second
forwarded-headers gap surfaced by actually tracing the real two-hop request
path (host nginx → `web`'s own internal nginx → `api`): `web`'s nginx was
overwriting `X-Forwarded-Proto` with its own single-hop `$scheme` instead
of passing through the outer proxy's already-correct value — fixed with a
`map` that passes through when present. Verified with a full local run in
actual `Production` mode (this app's first time ever booting outside
Development) under a throwaway compose project, then torn down cleanly.
Full detail: `.claude/history/2026-09-17-1500-biblioteka-svara-bg-deploy-files.md`.

### 10. The actual live deploy, and a real bug caught only by testing the deployed system (2026-09-17)
Drove the whole deploy over SSH (access confirmed working from this
environment; the owner supplied the real Postgres/admin passwords directly
in chat). Checked svara.bg's health before and after every single step.

Two more bugs, both found live, neither visible from reading the code in
isolation:
- **`db-backup` crash-looping** — `git archive` on the Windows dev machine
  (`core.autocrlf=true`) silently converts LF→CRLF in shell scripts on
  output, even though the stored git blobs are clean LF. Fixed with
  `.gitattributes` (`*.sh text eol=lf`), not a server-side patch, so the
  repo and the deployed server stay provably identical. A second gotcha on
  the same bug: recreating the container wasn't enough to pick up the fix —
  a single-file bind mount can pin to the old inode; needed
  `--force-recreate`.
- **Missing `Secure` cookie flag despite real HTTPS** — the morning's
  `ForwardedHeadersMiddleware` fix (#8) was necessary but insufficient.
  Isolated methodically (tested the API directly with a hand-set
  `X-Forwarded-Proto` header, bypassing both nginx hops, and the flag was
  *still* missing — proving the bug lived in the API's own middleware
  config). Root cause: `KnownIPNetworks = { }` / `KnownProxies = { }` in a
  C# object initializer doesn't clear an already-populated collection
  property — it's `.Add()`-per-element syntax, and with zero elements it's
  a silent no-op. The framework's loopback-only default was never actually
  cleared, and in this deployment's real topology `api`↔`web` traffic
  crosses the Docker bridge network, never loopback. Fixed with explicit
  `.Clear()` calls. Full detail (both bugs):
  `.claude/history/2026-09-17-1600-biblioteka-svara-bg-live-and-secure-cookie-fix.md`.

Site is live, TLS via Let's Encrypt (auto-renewing), login verified with
real production credentials, `Secure` flag confirmed present on the session
cookie.

### 11. One-time local → production data migration (2026-09-17)
Production started empty (new database, no data ever copied). Asked the
owner whether they wanted continuous two-way sync or a one-time cutover;
they chose the latter — production becomes canonical, local dev is
disposable going forward. `pg_dump` excluding `asp_net_*` (so the restore
couldn't clobber the production admin's real credentials with local dev
defaults), `pg_restore --clean --if-exists`, then an explicit SQL remap of
every `user_id` column across 13 tables (local admin's id → production
admin's id — this app scopes everything per-user, so skipping this would
have left 279 books in the database but invisible to the only account that
can log in). Cover images copied separately (files on a volume, not
database rows). Verified end to end: production login, `totalCount: 279`
via the real API, an actual cover loading over HTTPS. Full detail:
`.claude/history/2026-09-17-1700-local-to-production-data-migration.md`.

### 12. Show/hide password toggle on the login page (2026-09-17)
Small, no history file of its own until now: an eye-icon button toggling
the password `<input>` between `type="password"`/`type="text"`, inline SVG
icons (no new dependency), `aria-pressed`/`aria-label` wired up, i18n keys
in both locales. Built, tested locally, then deployed to production the
same way as everything else that day (`git archive` → `scp` → `docker
compose ... --no-deps web`).

## What's true right now

- **`https://biblioteka.svara.bg` is the live, canonical MyDigitalLibrary.**
  279 books, real covers, real wishlist, TLS, login working.
- **Local dev (`docker compose up` in this repo) is disposable** — useful
  for developing and testing changes, but its data no longer matters and
  shouldn't be assumed current. Don't expect a future "push local to prod"
  to be as simple as the one-time migration in §11 if local has since
  diverged — it would need the same table-by-table care again.
- **svara.bg was checked healthy after every single change today** and was
  never modified — only read from, to confirm nothing broke.
- Deploys to production are still manual (`git archive`/`scp`/`ssh`, see
  `deploy/README.md`) — no CI/CD pipeline for this app yet, not asked for.
- Minor, non-blocking items noted along the way, still open: ASP.NET Core's
  Data Protection key isn't itself encrypted at rest on disk (low
  incremental risk — only matters if the server's filesystem is already
  compromised); no HSTS header or CSP configured yet.
- `LibraryItemService.CreateAsync` still lacks the "existing `WorkId` + new
  `Edition`" path that `WishlistService.CreateAsync` already has — worked
  around during the Audible imports with a throwaway-wishlist-entry trick,
  documented in that history entry, not built as real API surface.

## Why this file exists

Nothing here is new work — it's a deliberate index over two days and twelve
distinct pieces of work, several of which depended on facts established
several exchanges earlier (the `svara-bg` deploy postmortems, the format-
aware duplicate rule, the `ForwardedHeaders` fix). A fresh thread picking
this project back up should be able to read this one file and know: what's
live, what's still manual, what almost went wrong and why, and where to
look for the full detail on any one piece — rather than needing to
reconstruct two days of conversation to avoid repeating a mistake that was
already found and fixed once.
