# Post-Stage-9 — импорт на реалната библиотека, четене/wishlist UI, категории, качване на корици

**Date:** 2026-09-12
**Context:** Not a numbered plan stage — this is user-driven follow-up work
after Stage 9 closed the plan's stage list (section 12). Triggered by the
owner actually starting to use the app: first a login-blocking bug report,
then "import my real shelf", then UI/UX feedback on the result, then two
more feature requests (categorization, cover upload).
**Status:** Complete — all of it deployed and verified against the real
`docker compose` stack (not just `dotnet test`); Angular dev server rebuilt
and manually smoke-tested via `curl` through the proxy for every new
endpoint. 91 real library items now live in the personal-use database.
**Commits:** all of the below landed in a single commit this session (see
`git log` for the hash) — unlike earlier stages, this wasn't built and
committed incrementally per sub-feature.

## What we built

### 1. Fixed the app being "practically unusable" on first load

Two compounding bugs, both pre-existing (not introduced this session):

- `proxy.conf.json` only proxied `/api` and `/health` — never `/covers`, so
  the Angular dev server 404'd every cover-image request. Separately, the
  i18n JSON files lived in `src/assets/i18n/` but `angular.json`'s `assets`
  glob only copies `public/`, so **every translation lookup 404'd too**,
  leaving the login form's title/labels/button permanently blank.
- That translation failure fed a self-sustaining loop: `error.interceptor.ts`
  showed a toast notification for the failed i18n request; the notification
  component itself renders through `TranslocoPipe`; the pipe's failed lookup
  triggered another load attempt; each attempt 404'd and toasted again —
  hundreds of stacked notification cards and ~100 req/s in the Network tab,
  which is what the "тъмни кутийки с ×" in the bug report actually were.

Fixed by moving `src/assets/i18n/` to `public/assets/i18n/` (the convention
the new Angular builder already used for `favicon.ico`), adding `/covers` to
`proxy.conf.json`, and hardening `error.interceptor.ts` to skip
`/assets/*` requests so a future missing asset can't re-trigger the same
notify → re-render → re-request cycle.

### 2. Imported the owner's real physical library — 91 books

Given 11 photos of physical shelves, transcribed every readable spine,
researched each one (five parallel background research agents — general iOS,
politics/hacking, English pop-science shelf, Bulgarian translated pop-science
shelf, and a mixed cleanup batch) for accurate title/author/publisher/ISBN
and a verified, hotlink-safe cover image URL, then bulk-imported via the
existing composite `POST /library-items` endpoint using a small Node script
(`import.mjs`) rather than one-by-one through the UI.

- 91 of ~135 transcribed spines were confirmed and imported; the rest were
  reported back to the owner in a filterable status table (a published
  Artifact) grouped by photo, with exact photo+row references for anything
  genuinely illegible.
- Two research corrections caught mid-import: a misread author name
  ("Карайотиев" → actually "Карагеоргиев"), and a fabricated co-author
  ("Michael Chan" on *How Innovation Works* — Matt Ridley has no such
  co-author on that title; likely confused with Alina Chan from his other
  book *Viral*).
- Deleted the two seed placeholder items (*Foundation*, *Dune*) first; later
  found and cleaned up their orphaned Work/Edition rows too (deleting a
  `LibraryItem` doesn't cascade to its Edition/Work).

### 3. Redesigned the library list and detail pages

The owner liked the warm-paper/serif look of the status Artifact from part 2
and asked for "something similar" on the real site. Introduced a small
design-token layer (`styles.scss`: `--bg`, `--surface`, `--accent`,
per-status pill colors, `Fraunces` + `Source Sans 3` via Google Fonts) and
rebuilt:

- **List page** — a stats bar backed by the *existing* `GET /statistics`
  endpoint (total, owned/borrowed/lent-out counts, currently-reading count —
  no invented numbers), and a card grid with real cover thumbnails
  (previously: a bare text list, no images anywhere, despite `coverImageUrl`
  already existing on the DTO and simply never being rendered).
- **Detail page** — a large cover, status pills, and every field the old
  plain `<dl>` had, restyled.
- Bumped the list's effective page size to 100 (was defaulting to 50) so all
  91 items actually render — a stopgap noted as needing real pagination if
  the collection keeps growing past ~150-200.

### 4. Reading status (start/finish dates, Goodreads-style)

The backend already had a full reading-session domain
(`ReadingSession`/`ReadingStatus`/progress entries) from Stage 7, entirely
unused by any UI. Rather than build the full page/percent progress tracker,
the owner asked for "the simple version, but with dates" — so:

- `LibraryItemDto` gained `ReadingStatus`/`ReadingStartedOn`/`ReadingEndedOn`,
  computed server-side from each item's *most recently started*
  `ReadingSession` (a new `LibraryItemService.GetLatestReadingInfoAsync`
  batch lookup, same shape as the existing edition-display-info stitching).
  No new table, no new write endpoint — purely a read-side projection over
  data that already existed.
- Angular detail page: a status pill (Unread/Reading/Finished/Abandoned/
  OnHold) plus date inputs and Start/Finish buttons that call the *existing*
  `POST /reading-sessions` and `POST /reading-sessions/{id}/finish`.
- Same status pill surfaced in the list-page cards too.

### 5. Wishlist UI (new — the backend endpoints existed, no frontend did)

"Not bought" was mapped onto the existing (but entirely UI-less) Wishlist
feature rather than inventing a second status field on `LibraryItem`. Added
`/wishlist` (list) and `/wishlist/add` routes, a nav link, and a
`WishlistApiService`. Deliberately scoped out for now: a one-click "mark as
bought" action — the existing `POST /wishlist/{id}/fulfill` endpoint requires
an *already-cataloged* `EditionId`, which a plain wishlist entry (just a
title/author) doesn't have; wiring that properly needs either an edition
picker or extending `fulfill` to accept nested edition creation like
`library-items` does. Flagged, not built.

### 6. Cover image upload/replace

The owner wanted to fix covers the research agents got wrong, by uploading
their own photo. The storage mechanism already existed
(`ICoverStorage`/`LocalFileCoverStorage`, SHA-256-named files on a Docker
volume) but was only reachable from the background download queue, never
from a direct upload. Added `POST /api/v1/editions/{id}/cover`
(`multipart/form-data`, `.DisableAntiforgery()` + the project's own
`AntiforgeryFilter` since the endpoint is form-bound and would otherwise
also trigger ASP.NET Core's separate built-in antiforgery check) calling
`EditionService.UploadCoverAsync` synchronously — no queue needed, the bytes
are already local. Wired into both the add-book form (uploads right after
create, using the returned `editionId`) and the detail page ("Смени
корицата").

`ICoverStorage` moved from `Infrastructure.Import.Covers` to
`Application.Abstractions` in the process — it was a port the Application
layer needed to depend on (for the new upload path) but had been defined in
Infrastructure, which Application must never reference directly. Same
pattern `ICoverDownloadQueue` already used; this just brought `ICoverStorage`
in line with it.

### 7. Genre/category system + language grouping

`Genre` (domain entity, `genres` table, `Work.GenreIds`) existed since Stage
1 but had zero application/API wiring — no endpoint, no way to ever set one.
Wired it the same way authors already work (resolve-or-create by name):

- `GET /api/v1/genres`, `GenreNames` added to `CreateWorkRequest` and
  `UpdateWorkRequest`, `BookCatalogService.SetGenresAsync` for the
  replace-all-genres case an edit does.
- `EditionDisplayInfo`/`WorkDisplayInfo` extended with `Language` and
  `GenreNames`; threaded through to `LibraryItemDto` (also gained `WorkId`,
  needed by the frontend to call `PUT /works/{id}` for genre corrections
  without a second lookup).
- Bulk-categorized all 91 imported books into exactly the two categories the
  owner asked for — **Научна литература** (50 books) and **Политика и
  история** (35 books) — via a one-off Node script matching on exact title.
  6 books didn't fit either bucket honestly (Mitnick's four hacking/security
  books, an Agatha Christie novel, a Linux pentesting manual) and were left
  uncategorized rather than forced into the wrong bucket.
- List page now groups client-side: language first (bg/en, unset last), then
  genre within each language (unset last) — computed from data already in
  one page load, no new query params needed at this collection size.
- Add-book form and detail page both got a language selector / genre
  checkboxes (the detail page's genre editor fetches the full `WorkDetail`
  first so saving a genre correction can't accidentally null out a title/
  description/year the DTO doesn't otherwise carry).

### 8. Incidental infrastructure fix: sessions surviving a redeploy

Discovered mid-session: every `docker compose up -d api` (needed after each
backend change, several times this session) silently logged the browser
out, because ASP.NET Core's Data Protection key ring had nowhere persistent
to live and regenerated on every container recreate, invalidating every
signed cookie. Added `AddDataProtection().PersistKeysToFileSystem(...)`
pointed at a new `dpkeys` Docker volume (mirroring how `covers` already
works) — verified by hitting an authenticated endpoint with a pre-restart
cookie after a fresh `docker compose up -d api` and getting `200`, not `401`.

## Why we built it this way

**Read the actual images, don't guess from filenames.** All 91 books came
from genuinely transcribing shelf photos (including upside-down and
partially-obscured spines) and only importing what could be independently
verified — six background research agents cross-checked every title/author/
ISBN/cover against real bookstore and library sources rather than trusting
the first guess, and two real transcription errors were caught and corrected
this way instead of silently shipped.

**Reuse existing domain machinery before adding new fields.** Reading status
and wishlist both mapped onto backend features that had been fully built in
earlier stages and never exposed — the fix in both cases was wiring, not new
domain design. Genre followed the same rule: the entity, table, and
`Work.AddGenre`/`RemoveGenre` methods already existed; only the
resolve-or-create service method and the endpoint were missing, copied
directly from the pattern authors already used.

**Ports belong in Application, not Infrastructure, even for one caller.**
Moving `ICoverStorage` was a small refactor forced by a real new dependency
direction (Application's `EditionService` needed to call it), not a
speculative cleanup — the project's layering rule (`IApplicationDbContext`,
`ICoverDownloadQueue`) already established exactly where this interface
belonged.

**Categorize honestly, including "doesn't fit."** Given only two requested
buckets, six books that are true-crime/hacking memoirs or straight fiction
were left uncategorized rather than jammed into the nearer-sounding bucket —
consistent with the same "ask when unsure, don't guess" discipline the
import phase used for illegible spines.

## Files created/changed

- `src/web/proxy.conf.json` — added `/covers`
- `src/web/public/assets/i18n/{bg,en}.json` — moved from `src/assets/i18n/`;
  new keys for reading status, wishlist, genre/cover form fields
- `src/web/src/app/core/http/error.interceptor.ts` — skip `/assets/*`
- `src/web/src/styles.scss`, `src/web/src/app/app.{html,scss}` — design tokens, fonts, shell restyle
- `src/web/src/app/features/library/library-list/*`, `library-detail/*`, `library-form/*` — redesign + reading/genre/cover UI
- `src/web/src/app/features/library/reading-api.service.ts` — new
- `src/web/src/app/features/wishlist/**` — new feature (list + add pages, routes, API service)
- `src/web/src/app/core/api/{models.ts,catalog-api.service.ts}` — new DTO fields, genres/cover/work-update client
- `src/MyDigitalLibrary.Application/Abstractions/{IApplicationDbContext.cs,ICoverStorage.cs}` — `Genres` DbSet, `ICoverStorage` moved here
- `src/MyDigitalLibrary.Application/{LibraryItems,Catalog,Works,Editions}/*` — reading-info projection, genre resolve-or-create, `WorkId`/`Language`/`GenreNames` on DTOs, `UploadCoverAsync`
- `src/MyDigitalLibrary.Api/Endpoints/{EditionEndpoints.cs,GenreEndpoints.cs}` — cover upload endpoint, new genre listing endpoint
- `src/MyDigitalLibrary.Api/Program.cs` — `AddDataProtection().PersistKeysToFileSystem(...)`, `MapGenreEndpoints()`
- `docker-compose.yml` — new `dpkeys` volume
- `.gitignore` — `/keys/`

No EF migration was needed — `genres`/`Work.GenreIds`/`reading_sessions`/
`wishlist_entries` all already existed in the schema from earlier stages;
this work only added application/API code around them.

## What's still open

- Wishlist has no one-click "mark as bought" yet (see part 5) — needs either
  an edition picker in the UI or a nested-edition-creation variant of
  `POST /wishlist/{id}/fulfill`.
- List pagination is a `pageSize=100` stopgap, not real paging.
- 6 imported books remain genre-less (true crime/hacking/fiction, don't fit
  the two requested categories) and ~40 transcribed-but-unconfirmed spines
  from the original 11 photos are still sitting in the status Artifact,
  waiting on the owner to confirm titles/authors before they can be
  imported.
- No automated test coverage was added for any of this session's new
  backend code (genre resolve-or-create, reading-info projection, cover
  upload) — verified manually against the real running stack instead,
  which is weaker than the project's normal integration-test discipline for
  new endpoints.

## Next

Nothing queued. Waiting on the owner to (a) confirm the remaining unclear
book spines from the status Artifact, and (b) decide whether the wishlist
fulfill flow is worth building out next.
