# Wishlist redesign: filters/sort/edit/delete/fulfill, and a title-search cover backfill

**Date:** 2026-09-13
**Context:** The owner looked at the "Искам я" (wishlist) page — 148 entries,
every one a plain grouped list with no filtering/search/sort, no way to edit
or delete an entry, no way to move a wanted book into the owned library once
bought, all missing cover images, and a "Priority: 3" pill nobody could
decode. Four separate asks in one message: bring the page up to parity with
the library list (filter/sort/search/edit), let entries move from "want" to
"have", explain/fix the priority display, and add scroll-to-top/bottom
buttons. A follow-up message then asked, explicitly, to actually go find
cover images for the 148 entries rather than leave that as a manual per-book
action.
**Status:** Complete. Backend + frontend built and verified against the live
`docker compose` stack (API rebuilt/restarted mid-session); domain/application/
integration test subsets green. The bulk cover backfill was run for real
against the owner's live data: 22/148 entries got a real, downloaded cover.
**Commits:** this session's commit (see `git log`).

## 1. Backend: wishlist went from 3 endpoints to a real CRUD surface

Before this session `WishlistService`/`WishlistEndpoints` only had
`ListAsync` (GET /), `CreateAsync` (POST /), and `FulfillAsync` (POST
/{id}/fulfill) — no way to read one entry, edit one, or delete one. Added:

- `GetAsync` — `GET /api/v1/wishlist/{id}`, needed by the new edit/fulfill
  pages (they can't render off the bulk list endpoint alone).
- `UpdateAsync` — `PUT /api/v1/wishlist/{id}`, wraps the domain's existing
  `WishlistEntry.Update()` (format/priority/preferredEditionId/maxPrice/
  note/isOutOfStock) — the domain method already existed and was simply
  never exposed.
- `DeleteAsync` — `DELETE /api/v1/wishlist/{id}`.
- **Correctness fix, not a new feature:** `ListAsync` now filters
  `!w.IsFulfilled`. `Fulfill` is documented as a one-way transition (an
  entry becomes a `LibraryItem`), but the list endpoint never excluded
  fulfilled entries — harmless before this session because nothing ever
  called `FulfillAsync` from the UI, but wiring up the new "I already have
  it" button would have started leaving completed wishes stuck in the list.

All three new endpoints require the antiforgery header like every other
mutating endpoint (`AddEndpointFilter<AntiforgeryFilter>()`).

## 2. Frontend: wishlist list rebuilt to match the library list's UX

`wishlist-list.page.ts/html/scss` — previously a bare grouped list, now
mirrors `library-list` structurally: debounced search (client-side, over the
already-fetched 148 entries — no server-side query param exists for
wishlist), genre/format/priority filters, an out-of-stock quick-filter stat
tile, sort by added-date/title/author/priority with direction toggle,
collapsible language→genre groups (state keyed by language/genre key, not
label, so it survives a UI-language switch), and the same fixed
scroll-to-top/scroll-to-bottom button pair the library list already had
(`library.list.scrollToTop/Bottom` keys reused, no new translations needed
there).

Each card now has three actions: **Редактирай** (→ new `wishlist-edit`
route), **Вече я имам** (→ new `wishlist-fulfill` route), **Изтрий** (calls
`DELETE`, confirms via `window.confirm`, then `listResource.reload()`).

### New page: wishlist-edit

`wishlist-edit.page.ts/html/scss` — three sections:
- **Cover** — shows the current cover (or placeholder) plus an ISBN/link
  search box reusing the *existing* `ImportApiService.lookup()` (the same
  mechanism `/import` already used for library items, never before wired to
  wishlist). Confirming a candidate calls the *existing* `POST
  /api/v1/works/{workId}/editions` (already used by `WorkService.AddEdition`,
  never before called from the wishlist feature) with the candidate's
  `coverUrl`, then `PUT`s the wishlist entry's `preferredEditionId` to the
  new edition. No new backend surface needed for this path — every piece
  already existed for the library-item flow, just never connected to
  wishlist entries.
- **Work** — title/authors, via the existing `catalog.updateWork`.
- **Wish** — format/priority/max price/note/out-of-stock, via the new `PUT`.
- Delete button at the bottom (same confirm as the list page).

### New page: wishlist-fulfill

`wishlist-fulfill.page.ts/html/scss` — acquisition form (date/method/price/
source, plus room/shelf/box when the desired format is Physical). On submit:
if the entry already has a `preferredEditionId`, fulfill directly; otherwise
first create a bare edition (no ISBN, just the desired format) via the same
`createEdition` call, *then* fulfill with that edition's id — `FulfillAsync`
has always required an existing `EditionId`, and most entries never had one.
Navigates to `/library/{newItemId}` on success.

### Priority, explained not just relabeled

"Priority: 3" was meaningless because (a) the add form defaulted every entry
to 3 and most of the 148 were bulk-imported without ever touching that
field, and (b) the list only ever showed the bare number. Added
`wishlist.priorityLevel.1`..`5` (Най-висок/Висок/Среден/Нисък/Най-нисък) and
a `priorityLabel()` helper used everywhere the number appears (list pills,
edit form, add form) — the add/edit forms' number input became a `<select>`
so the scale is visible at the point of choosing, not just after the fact.

## 3. Cover backfill — the harder half

Every existing entry had `coverImageUrl: null` **by construction**, not by
bug: the wishlist add form only ever sent `work` (title/authors/etc, no
`edition`), so `CreateAsync`'s "no WorkId" branch created a bare `Work` with
no `Edition` at all — and cover images live on the edition. The `/import`
page's ISBN/link lookup was the only place in the app that ever attached a
cover, and it only ever fed into `POST /library-items`, never `POST
/wishlist`.

The owner then asked, explicitly, to just go find covers for the 148 rather
than leave it manual. That needed a genuinely new capability: title/author
search (not ISBN lookup), since none of these entries have an ISBN to look
up by.

- **`IBookMetadataProvider`** gained a second method,
  `SearchAsync(title, authorNames, ct)`, alongside the existing
  `LookupByIsbnAsync` — same never-throws contract.
- **`GoogleBooksProvider`**: refactored `FetchAsync` into a shared
  `FetchVolumesAsync(query, logSubject, ct)` so ISBN lookup
  (`q=isbn:{isbn}`) and the new search (`q=intitle:{title}+inauthor:{author}`)
  share one HTTP/error-handling path.
- **`OpenLibraryProvider`**: added a genuinely new method against
  `/search.json?title=...&author=...` (the existing `LookupByIsbnAsync` used
  the *different* `/api/books?bibkeys=...` endpoint, whose response shape
  doesn't carry a title-search result) — new `OpenLibrarySearchResult`/
  `OpenLibrarySearchDoc` types, cover built from `cover_i` as
  `https://covers.openlibrary.org/b/id/{id}-L.jpg`.
- **`CompositeBookMetadataProvider.SearchAsync`**: unlike the ISBN path
  (which *merges* every provider's fields), search tries providers in
  registration order — Open Library first, Google Books second — and
  returns the first candidate that actually has a cover, rather than
  blending fields from what might be two different books/editions. Open
  Library first isn't just already-registration-order convenience: Google
  Books' anonymous quota was observed exhausted (429) two days before this
  session (see the existing comment in `GoogleBooksProvider`), so preferring
  the unlimited source first measurably reduces exposure to that quota
  across ~150 calls.
- **`WishlistService.FindCoverAsync`** (→ `POST
  /api/v1/wishlist/{id}/find-cover`): no-ops if the entry already has an
  edition; otherwise searches by the work's title/first author, and —
  critically — **rejects the candidate if the title doesn't actually
  resemble the entry's**, via a loose word-overlap check
  (`TitleLooksLikeAMatch`: lowercased, punctuation-stripped, ≥3-letter
  words, requires at least half of the shorter title's words present in the
  other). A free-text match is inherently uncertain in a way an ISBN lookup
  isn't; this is the guard against pinning a wrong book's cover onto a real
  entry. Never trusts an ISBN from the search result either — a matched
  title's ISBN names one specific edition that wasn't independently
  verified, so the created edition always has `Isbn13: null`.
- Response includes both the persisted entry *and* the raw external
  `coverUrl` — the created edition's cover is downloaded by the existing
  background queue (`ICoverDownloadQueue`, unrelated to this session), so
  the locally-hosted `coverImageUrl` isn't populated synchronously; the raw
  URL lets the UI show something immediately.

### Frontend: bulk "find missing covers"

`wishlist-list.page.ts` gained a `findMissingCovers()` bulk action: a
sequential loop (not `Promise.all` — a title match is uncertain and each
call may hit the Google Books quota, so parallelizing would only make both
problems worse) with a 600ms gap between calls, a stop button, and a
live "checked X of Y, found Z" progress line. Each success optimistically
patches the specific entry's `coverImageUrl`/`preferredEditionId` in
`listResource.value` (Angular's resource `.value` is a `WritableSignal`,
confirmed against `@angular/core`'s type defs) without a full reload, so
covers appear as the run progresses instead of only at the end.

### Run against the real data

Rebuilt and restarted the `api` Docker container mid-session (`docker
compose build api && docker compose up -d api`) to pick up the backend
changes without touching the `db`/`db-backup` containers or the Postgres
volume. Logged in as the seed admin via curl to smoke-test `GET
/wishlist`, `GET /wishlist/{id}`, and a no-op `PUT` against live data before
trusting the new endpoints. Then ran the actual backfill — a small Node
script (`find-covers.mjs` in the session scratchpad, not committed) hitting
the new endpoint for all 147 cover-less entries with the same 600ms
throttle used in the UI — end to end:

**Found: 21 covers** (22 counting the one tested manually beforehand) — On
the Origin of Species, The Voyage of the Beagle, The Malay Archipelago, Our
Human Story, Freezing Order, A Brief History of Time, Clean Architecture,
Design Patterns, Head First Design Patterns, Cosmos, and others — almost
entirely English-language science/tech titles.
**Not found: 126** — almost entirely Bulgarian-language history/politics/
memoir titles that Open Library and Google Books simply don't index, not a
search-logic failure.
**Errors: 0** — the antiforgery token held for the full ~15-minute run, and
every provider timeout/429 was absorbed as "no result" per the existing
never-throws contract, never surfacing as a hard failure.

Verified afterward against the live API that the 22 `coverImageUrl` values
are real, already-downloaded local paths (`/covers/<hash>.jpg`), not just
queued external URLs.

## Why we built it this way

**Reuse the exact mechanism the library-item flow already had, rather than
inventing a second cover pipeline for wishlist.** The ISBN/link lookup,
`POST /works/{id}/editions`, and the cover-download queue all predate this
session and already worked correctly for library items — wishlist entries
were simply never wired to any of it. Every new capability here (edit's
cover search, fulfill's edition creation, the bulk backfill) is that same
mechanism called from a new place, not a parallel implementation.

**A free-text match needs a rejection path, not just a "best guess."**
Nothing forced the title-similarity check — Google Books/Open Library
return *a* result for almost any query, right or wrong. Without
`TitleLooksLikeAMatch`, "opitay da im namerish snimki" risked silently
pinning wrong covers onto real books with no easy way for the owner to
notice which ones were wrong. Reporting `Found=false` when the match looks
bad — leaving that book for the manual per-book search already built into
the edit page — was worth the accuracy cost of possibly-real matches that
happen to word-overlap poorly with a translated/reformatted title.

**Throttle preemptively, not reactively, against a quota already seen
exhausted.** The 429 comment already sitting in `GoogleBooksProvider` from
two days before this session ("verified 2026-09-11") was the signal to
design around the shared anonymous quota from the start (Open-Library-first
ordering, a deliberate per-call delay) rather than discover it mid-backfill
against real data with no way to know which of the 148 got skipped because
of it versus genuinely not existing in either source. The run's 0 errors
and clean found/not-found split back this up in retrospect.

## Files changed

Backend:
- `src/MyDigitalLibrary.Api/Endpoints/WishlistEndpoints.cs` — GET/PUT/DELETE/find-cover routes
- `src/MyDigitalLibrary.Application/Wishlist/WishlistService.cs` — GetAsync/UpdateAsync/DeleteAsync/FindCoverAsync, fulfilled-filter fix
- `src/MyDigitalLibrary.Application/Wishlist/WishlistEntryDto.cs` — UpdateWishlistEntryRequest, FindCoverResultDto
- `src/MyDigitalLibrary.Application/Import/IBookMetadataProvider.cs` — SearchAsync contract
- `src/MyDigitalLibrary.Application/Import/CompositeBookMetadataProvider.cs` — SearchAsync (first-with-cover, not merged)
- `src/MyDigitalLibrary.Infrastructure/Import/GoogleBooksProvider.cs` — shared FetchVolumesAsync, SearchAsync
- `src/MyDigitalLibrary.Infrastructure/Import/OpenLibraryProvider.cs` — /search.json SearchAsync, new response types

Frontend:
- `src/web/src/app/features/wishlist/wishlist-list/wishlist-list.page.{ts,html,scss}` — full redesign, bulk find-covers
- `src/web/src/app/features/wishlist/wishlist-edit/` — new page (cover search, work/wish sections, delete)
- `src/web/src/app/features/wishlist/wishlist-fulfill/` — new page (acquisition → library)
- `src/web/src/app/features/wishlist/wishlist-form/wishlist-form.page.{ts,html}` — priority select + label
- `src/web/src/app/features/wishlist/wishlist-api.service.ts` — update/delete/fulfill/findCover
- `src/web/src/app/features/wishlist/wishlist.routes.ts` — :id/edit, :id/fulfill
- `src/web/src/app/core/api/models.ts` — UpdateWishlistEntryRequest, FulfillWishlistEntryRequest, FindCoverResult, CreateStandaloneEditionRequest
- `src/web/src/app/core/api/catalog-api.service.ts` — createEdition
- `src/web/public/assets/i18n/{bg,en}.json` — wishlist.* additions, library.location.*

## What's still open

The 126 Bulgarian-titled entries with no cover need the manual per-book
ISBN/link search (already built into wishlist-edit) if the owner wants them
filled in — nothing left to automate there without a source that actually
indexes Bulgarian-market books.

## Next

Nothing queued.
