# Post-Stage-9 — search/filter fixes, full book editing, bilingual categories, anti-forgery bug

**Date:** 2026-09-12
**Context:** Not a numbered plan stage — a second round of user-driven
follow-up after the owner actually used the app day-to-day: a bug report
("Нещо се обърка" on saving, a garbled "??????" category, checkbox-only
editing) plus feature requests (better search, filter/sort, full editing,
bilingual categories), refined over several rounds of live feedback
(screenshots of real bugs in the actual browser session) rather than a
single upfront spec.
**Status:** Complete — all of it deployed and verified against the real
`docker compose` stack, not just `dotnet test`. Every backend change was
smoke-tested through the live API against the owner's real 91-book library
before being called done; the two live-verification mistakes made along
the way (below) were caught and fixed in the same session.
**Commits:** landed in a single commit this session (see `git log` for the
hash) — the whole arc, from investigation through the last live fix, was
one continuous piece of work.

## What we built

### 1. Fixed the recurring "Нещо се обърка. Опитай пак." save error

Root-caused, not just papered over. Two compounding bugs:

- `AntiforgeryFilter.cs` returned `Results.Problem(...)` on a rejected
  anti-forgery token **without** the `errorCode` extension every other
  error in the app sets — so the frontend's `error.interceptor.ts` couldn't
  match it to the translation key `errors.antiforgery.invalid_token`
  (which already existed in `bg.json`/`en.json`, unreachable) and fell back
  to the generic message.
- `AntiforgeryService` (`antiforgery.service.ts`) caches the token for the
  entire SPA session (`shareReplay(1)`); its `invalidate()` method existed
  but was **never called anywhere** (confirmed by a repo-wide grep). Once a
  token went stale — cookie rotation, a backend restart during
  `docker compose up -d api`, a long idle tab — every mutating request
  failed forever until a hard page reload.

Fixed both ends: the filter now sets `errorCode`, and
`error.interceptor.ts` catches that specific code, calls
`antiforgery.invalidate()`, fetches a fresh token, and retries the original
request once before ever showing a toast — turning a hard failure into an
invisible self-heal.

### 2. Search — case sensitivity, then word-boundary matching

Two rounds, because the first fix wasn't actually right:

- **Round 1**: `q=габо` matched nothing against "Габор Мате" because
  Postgres `LIKE`/`.Contains()` is case-sensitive by default. Switched to
  `EF.Functions.ILike`.
- **Round 2** (owner feedback): `q=мат` was matching "**Фермата** на
  животните" — a real substring, but from the *middle* of a word, which
  the owner correctly called out as wrong for "do I already own this"
  lookups. Replaced `ILIKE '%q%'` with a Postgres regex using the `\m`
  ("start of word") anchor, so a query only matches at a word boundary —
  "мат" now finds "Мате"/"Мат Ридли" but not "фермата".
  - Hit an Npgsql quirk along the way: `Regex.IsMatch(field, pattern)`
    translates to `field ~ ('(?p)' || @pattern)`, and Postgres's regex
    engine rejects two embedded-option groups back to back — so
    hand-embedding `(?i)` in the pattern string (for case-insensitivity)
    produced `(?p)(?i)...` and errored with "quantifier operand invalid".
    Fixed by passing `RegexOptions.IgnoreCase` as a real parameter to
    `Regex.IsMatch` instead, letting Npgsql fold it into its own prefix.
  - Extracted the pattern-building logic to
    `Application/Common/TextSearch.cs` since both `LibraryItemService` and
    `WorkService` needed the identical fix.
- Search now matches **title and author together** (was title-only before
  this session) and reacts live as the owner types (existing `httpResource`
  reactivity), with a 250ms debounce added so fast typing doesn't fire a
  request per keystroke.

### 3. Filter and sort on the library list

`LibraryItemService.ListAsync` and its endpoint gained `genreId`,
`readingStatus` (including a synthetic `NotStarted` — "no reading session
yet", derived rather than stored, matching how `ReadingStatus` is already
projected), `sortBy` (`title`/`acquiredOn`) and `sortDir`. Wired into three
new `<select>`s next to the search box; picking a sort switches the list
from the existing language→genre grouped view to a flat sorted one.

### 4. Full book editing (was cover-only)

New page, `/library/:id/edit`, linked from a "Редактирай" button on the
detail page (owner's choice — a separate page, not inline fields). Three
independently-saveable sections matching the three existing PUT endpoints
so fixing just the ISBN doesn't require touching acquisition fields too:

- **Work**: title, original title, description, first publication year,
  authors, genres → `PUT /works/{id}`.
- **Edition**: ISBN, publisher, translator, publication year, page count,
  cover type, narrator/duration (shown conditionally on the edition's
  fixed `Format`) → `PUT /editions/{id}` (already existed, just never
  called from the UI).
- **Acquisition + status**: acquired-on, method, price, source, ownership
  status → the existing note-update PUT plus `PATCH /status` (also
  pre-existing, unused until now).

`UpdateWorkRequest` gained `AuthorNames` (previously title/description/
genres only) — `WorkService.UpdateAsync` now diffs and syncs authors via a
new `BookCatalogService.SetAuthorsAsync`, the same resolve-or-create
pattern `SetGenresAsync` already used.

Cleaned up a pre-existing bug while touching `models.ts`: `WorkDetail` was
declared **twice** in the same file (harmlessly merged by TypeScript's
interface declaration merging, but confusing) — consolidated to one.

### 5. Category management — rename/delete, and a real fix for "??????"

The owner reported a category rendering as literal `??????`. Investigated
properly before touching anything:

- Queried the genres table directly — the stored bytes are literal ASCII
  `0x3f` ('?'), not a mis-decoded multi-byte sequence, so it wasn't a
  read-side encoding bug.
- Checked every migration, `DevelopmentSeeder`, and the CSV import path —
  none seed or touch genres.
- Read the prior session's own history doc
  (`2026-09-12-1026-library-import-catalog-reading-wishlist-covers.md`),
  which documents that the 91-book import created **exactly two**
  categories by name ("Научна литература", "Политика и история") via a
  one-off, never-committed Node script. No third category is mentioned
  anywhere — the corrupted one wasn't part of that documented work, and its
  origin (almost certainly a terminal/shell encoding mismatch feeding
  Cyrillic text into a raw API call — see the near-miss below) is
  unrecoverable.
- Per the owner's explicit choice, didn't guess a replacement or delete it
  server-side — instead built the tool to fix it themselves: `GenreEndpoints`
  gained `PUT /genres/{id}` (rename) and `DELETE /genres/{id}` (detaches
  the id from every work's `GenreIds` array first, since that's a plain
  `uuid[]` column with no FK to cascade), and a new shared
  `GenrePickerComomponent` (checkboxes + inline rename/delete + a free-text
  "add new category" box) replaced the read-only checkbox lists on the
  add-book, edit, and detail pages — closing the loop that let "??????" get
  created through raw API use in the first place (there was previously no
  UI path to create a *new* category at all).

**Near-miss while verifying this live**: testing the rename endpoint via a
raw `curl -d '{"name":"Научна литература",...}'` in this environment's Git
Bash terminal silently mangled the Cyrillic literal into `??????` bytes
*before curl even sent the request* — reproducing, live, almost certainly
the exact mechanism that created the original corrupted category. Caught
immediately (compared the response against what was sent), fixed by
writing the JSON to a file with the Write tool (which preserves UTF-8
correctly) and using `curl --data-binary @file` instead of an inline
argument. No lasting damage — the real "Научна литература" genre (used by
50 of the owner's books) was restored within the same exchange.

### 6. Bilingual categories (owner-requested follow-up)

Added a `NameEn` column (`Genre.Rename` now takes both names; nullable,
falls back to the primary name when unset) via a new EF migration,
threaded through `GenreDto`/`RenameGenreRequest`/the rename endpoint. The
`GenrePickerComponent`'s rename UI now edits both fields at once (a save
always writes both — a bare rename with no English text supplied clears
any previous translation, matching the "replace all" convention the rest
of the app's PUT endpoints already use). `LanguageService` gained a
reactive `activeLangSignal` (previously an imperative-only getter) so
components can react to a language switch; a new
`shared/genre-display.ts` maps a genre (or a bare genre-name string, via
the loaded genre list) to its label in the active language. Wired into the
picker's checkboxes, the list page's genre filter dropdown, and the
list page's genre group headers.

## Why we built it this way

**Investigate before guessing, especially with data loss on the table.**
The "??????" category could have been "fixed" by inventing a plausible
Bulgarian word and moving on — instead, three separate sources (raw DB
bytes, every seed/migration/import path, the prior session's own history
doc) were checked to establish it's genuinely unrecoverable before
recommending an approach, and the recommendation was explicitly put to the
owner as a choice rather than assumed.

**A search fix isn't done when it compiles — verify against real data.**
Both search bugs (case sensitivity, then word-boundary) were caught
because the owner tested with their actual library, not synthetic test
data; both fixes were re-verified the same way afterward (curl through the
live API, reading back real titles) before being called complete, which is
also how the regex/Npgsql translation bug got caught before it reached the
owner.

**Self-healing over surfacing every failure.** The anti-forgery fix
specifically avoids just improving the error message — a token going stale
is an expected, recoverable event (not a real "session expired"), so the
interceptor retries transparently and only bothers the user if the retry
*also* fails.

**Keep the resolve-or-create identity separate from display.** `NameEn`
is purely a display overlay — genres are still resolved/created by the
primary `Name`, so adding bilingual support didn't touch the identity
model `genreNames` arrays across `LibraryItemDto`/`WorkDetailDto` already
depend on.

## Files created/changed

- `src/MyDigitalLibrary.Api/Filters/AntiforgeryFilter.cs` — `errorCode` on the rejected-token response
- `src/MyDigitalLibrary.Api/Endpoints/{GenreEndpoints.cs,LibraryItemEndpoints.cs}` — rename/delete genre endpoints, new list query params
- `src/MyDigitalLibrary.Application/Common/TextSearch.cs` — new, shared word-prefix pattern builder
- `src/MyDigitalLibrary.Application/LibraryItems/LibraryItemService.cs` — search rewrite, genre/reading-status filters, sort
- `src/MyDigitalLibrary.Application/Works/{WorkDto.cs,WorkService.cs}` — `AuthorNames` on update, same search fix
- `src/MyDigitalLibrary.Application/Catalog/BookCatalogService.cs` — `SetAuthorsAsync`
- `src/MyDigitalLibrary.Domain/Catalog/Genre.cs`, `Infrastructure/Persistence/Configurations/GenreConfiguration.cs` — `NameEn`
- `src/MyDigitalLibrary.Infrastructure/Migrations/20260912124227_AddGenreNameEn.*` — new migration
- `src/web/src/app/core/http/error.interceptor.ts` — anti-forgery retry-once
- `src/web/src/app/core/i18n/language.service.ts` — reactive `activeLangSignal`
- `src/web/src/app/core/api/{models.ts,catalog-api.service.ts}` — dedup `WorkDetail`, edition/genre-rename client methods
- `src/web/src/app/shared/genre-display.ts`, `shared/ui/genre-picker/**` — new, shared translated-label helper and picker component
- `src/web/src/app/features/library/library-edit/**` — new full-edit page
- `src/web/src/app/features/library/{library-list,library-detail,library-form}/**` — filters/sort UI, genre-picker adoption, edit-page link
- `src/web/public/assets/i18n/{bg,en}.json` — new keys throughout
- `tests/MyDigitalLibrary.Api.IntegrationTests/{Genres,LibraryItems}/**`, `Works/WorkUpdateEndpointTests.cs` — new, 19 new tests (backend total 106 → 125)

## What's still open

- The corrupted "??????" category is still sitting in the database,
  unused by any work — the owner now has rename/delete controls to deal
  with it whenever they choose to.
- Only "Научна литература" has an English translation set (`Non-fiction`,
  added as a demo while verifying the feature) — "Политика и история" and
  any future categories are untranslated until the owner sets them.
- Two anyComponentStyle CSS budget warnings (`library-detail.page.scss`,
  `library-list.page.scss`, ~0.6–1.4KB over the 4KB warning threshold) from
  this session's new UI — non-fatal, `ng build` still succeeds, flagged
  but not addressed.
- No pagination yet on the library list (still the `pageSize=100`
  stopgap from the prior session) — sort/filter reduce the practical need
  but don't remove it.
- The frontend's local `ng serve` dev server needed restarting twice this
  session (once for a stale route table after adding `/library/:id/edit`,
  once after it silently died, unrelated to the Docker containers which
  stayed healthy throughout) — a reminder that dev-server state and
  container state are independent and both worth checking when "nothing
  works."

## Next

Nothing queued. Waiting on the owner to try the fixed search/filter/edit
flows against their real library and report anything that still feels off.
