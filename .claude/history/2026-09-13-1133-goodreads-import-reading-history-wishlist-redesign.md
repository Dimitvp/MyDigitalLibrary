# Goodreads backfill, reading-history UI, wishlist redesign, quick-filter stats, catalog data cleanup

**Date:** 2026-09-13
**Context:** A long, multi-round owner session working directly against the
running `docker compose` stack (API + Postgres) and the local `ng serve` dev
server — a mix of small UI requests (screenshots with red arrows pointing at
what's missing), one large one-off data project (importing 160 years of
Goodreads reading history into the app), and several rounds of manual data
correction driven by the owner spotting mismatches (a book cover, an author's
name) against photos of their actual physical books.
**Status:** Complete — all code changes verified with `dotnet build` /
`ng build`, the API container rebuilt and restarted for every backend change,
all data operations executed against the live database and spot-checked with
`psql` afterward.
**Commits:** first commit for this session — everything below had been
sitting as uncommitted working-tree changes plus already-applied live data
mutations before now.

## What we built

### 1. Fixed books missing from the home page (pagination cap)

The library list hard-coded `pageSize=100` when the collection had grown to
197 books; anything past the 100th (sorted by acquisition date) silently
never rendered — reported as "Turkey Under Erdogan... doesn't show when I
scroll down." Bumped the frontend request to `pageSize=1000` and the
backend's `PageRequest.MaxPageSize` safety cap from 200 to 1000, so there's
real headroom above the current ~250-book collection.

### 2. Language visible on the book itself, not just under a group header

Added a `shared/language-display.ts` helper (`languageDisplayLabel`) and a
language "pill" next to each book's status/reading pills — on the library
list (both grouped and, later, the removed flat-sort view) and on the detail
page — so the language is visible on the item itself instead of only
implied by which language section it's filed under.

### 3. Library list: scroll-to-top/bottom, filter label, sort-within-category, collapsible groups

- Fixed-position ↑/↓ buttons (bottom-right) that smooth-scroll the page.
- Added a "Филтрирай по:"/"Сортирай по:" label in front of the
  filter/sort dropdowns (owner: "тук май трябва да пише нещо").
- Added a new **sort-by-author** option alongside the existing
  sort-by-title. Critically, sorting no longer flattens the
  language→genre grouping into an ungrouped list (the old behavior) — all
  sorting (including the pre-existing acquired-date default) now happens
  *inside* each genre bucket, computed entirely client-side since the whole
  ~250-item collection is already fetched in one page. This let the
  `sortBy`/`sortDir` query params be dropped from the list-items HTTP call
  entirely (the backend never supported sorting by author anyway).
- Every language section and every genre subsection got a ▾/▸
  collapse toggle, state tracked in two `Set<string>` signals keyed by
  language code / `${languageKey}::${genreKey}` (not by display label, so a
  UI-language switch doesn't reset what's collapsed).

### 4. Stat cards became clickable quick-filters

The five (later six) numbers at the top of the library page — total,
owned, borrowed, lent-out, currently-reading, and (added later) read — are
now `<button>`s. Clicking one narrows the list to just those books;
clicking the active one again clears back to "all". Required adding an
`ownershipStatusFilter` signal (the `status` query param already existed
server-side but the frontend never sent it) and, for the "Прочетени"
(read) card, a brand new **all-time** finished-book count —
`StatisticsDto.BooksFinishedTotal`, counting distinct library items with a
`Finished` reading session (a reread doesn't double-count), since the only
existing finished-count field was year-scoped (`BooksFinishedThisYear`).

### 5. Manually marking a book as already-read (with real start/end dates)

The only way to reach "Finished" status was live start-now/finish-now — no
way to backfill a book read in the past, which blocked everything in
sections 7–9 below. Added a "Вече си я прочел? Въведи кога:" mini-form next
to the existing start-reading control: two date inputs, one button, calls
`start` then immediately `finish` on the new session.

### 6. Full book metadata on the detail page

The detail page only showed format/acquisition/price/source. Added original
title, description, ISBN, publisher, publication year, page count, cover
type, translator, narrator, and duration (each only rendered when present)
— fetched via a new `editionResource` (`GET /api/v1/editions/{id}`) and the
already-loaded `workResource`.

### 7. Wishlist redesigned to match the library's visuals — separate dataset

The wishlist list page was a plain bulleted list; the owner wanted "the same
vision" (cover art, card grid, language/genre grouping) as the library list,
explicitly **without** merging the two datasets. Rebuilt the page around the
same card-grid/grouping markup and CSS, backed by the still-fully-separate
`/api/v1/wishlist` endpoint. This needed backend support that didn't exist:
`WishlistEntryDto` gained `CoverImageUrl`/`Language`/`GenreNames`, sourced
from `BookCatalogService.GetEditionDisplayInfoAsync` keyed by the entry's
(optional) `PreferredEditionId` — absent for most entries today, so most
wishlist cards currently fall into the "unset language" bucket until a
preferred edition is chosen; noted as a known limitation rather than solved
with a synthetic edition.

### 8. Goodreads reading-history backfill (160 books, one big scripted import)

The owner exported their Goodreads library (an artifact they'd built
earlier — "The Filing Queue", a 160-row checklist tool) and asked for it to
be reconciled against the ~197 physical books already in the app, using
their own stated invariant: *every physical copy is already entered, so
anything on the Goodreads list that's missing must have been read as an
ebook/audiobook, or is still wanted*.

Built a small offline pipeline (Node scripts in the session scratch
directory, not committed — this was one-off data work, not a repo feature):

1. Extracted the 160-book `BOOKS` array from the artifact's saved HTML.
2. Pulled the existing 197 items from Postgres (title/authors/ISBN/language)
   via `docker exec psql`.
3. Matched Goodreads rows to existing library items by ISBN13 first, then
   exact/fuzzy normalized-title + author-last-name matching — **27
   matched** (all verified by eye — author overlap ruled out every
   candidate false positive).
4. For the 133 unmatched: shelf `to-read` → destined for the wishlist;
   shelf `read`/`currently-reading` → destined for a *new* library item
   (format guessed from Goodreads' `binding` field where it clearly said
   Kindle/ebook/Audio, else defaulted to Ebook — flagged as an assumption
   in this doc, not silently swallowed).
5. Authenticated against the real running API (cookie + `X-XSRF-TOKEN`
   antiforgery flow, same as the SPA) rather than writing raw SQL, so every
   insert went through the same domain validation, resolve-or-create author/
   genre logic, and reading-session state machine the UI uses.
6. Flagged four genuinely ambiguous rows for the owner instead of guessing:
   a currently-reading book absent from the physical library (contradicts
   the invariant), a dubious "hacking course" Kindle listing, a 4-book
   Taleb bundle, and a 3-book boxed-set row two-thirds already accounted
   for individually.

**Result:** 24 existing items got a backfilled reading session (23
Finished, 1 Reading), 51 new Ebook-format items were created with their own
reading sessions, 78 new wishlist entries were created. Zero errors across
153 sequential API calls. 9 of the backfilled sessions used the Goodreads
"date added" as a stand-in single-day read date because "date read" was
blank — called out explicitly rather than presented as certain.

### 9. Follow-up corrections from the owner's replies to the flagged list

- **God Is Not Great**: split into two real items — an English audiobook
  (already finished, per the owner) and a new-but-unread Bulgarian physical
  copy — rather than one item that was neither.
- **Incerto 4-Book Bundle**: added as a single wishlist entry with an
  explanatory note (it's 4 separate Taleb books) plus the Goodreads URL the
  owner gave, so it makes sense the next time they look at it.
- **Hacking course listing**: added to the wishlist as asked, no further
  judgment applied.
- **In the Realm of Hungry Ghosts**: the initial import had guessed "Ebook"
  for this one (wrong — it was Audiobook, per the owner, who separately owns
  an unread physical copy). Format turned out to be **immutable** in this
  app (`Edition.Format` has no setter, and `EditionService.UpdateAsync`
  never touches it) — fixed with a direct, narrowly-scoped `UPDATE` on the
  `editions`/`library_items`/`reading_sessions` rows (format + clearing the
  now-invalid `page_count`) since no API path exists for changing an
  edition's format after creation. Then added the missing physical copy as
  a normal new item.
- **When The Body Says No / ... boxed set**: skipped per the owner's
  instruction; the two components already individually tracked
  ("Hold On to Your Kids" read physically, "Hungry Ghosts" per above) were
  left untouched since they were already correct.
- **Thinking in Bets**: finished this spring per the owner but never marked
  — closed the still-open Reading session with an approximate 2026-04-15
  end date (owner can correct the exact date via the detail page).

### 10. "1984" / "Фермата на животните" — the confusingly-entered pair

The owner pointed out the "1984" shown in the app was the English Signet
Classics edition, but the physical book they actually own and are reading
is a specific 2021 Siela (Сиела) hardcover, Bulgarian, translated by
Венцислав К. Венков — paired in one release with their existing Bulgarian
"Фермата на животните" (Animal Farm), whose catalog metadata (ISBN,
publisher) also turned out to be wrong for the copy they own.

- Corrected both editions' language/publisher/translator/year/ISBN/
  page-count/cover-type directly (rather than creating duplicate works —
  the owner's framing treated each as one entry to *fix*, not two).
- The exact ISBN/page-count for "1984" (9789542833734, 416pp) and a real
  cover image for both books were found via `WebSearch` + `WebFetch`
  against ciela.com (the Animal Farm ISBN the owner gave directly matched
  the same catalog series, confirming it was the right find) — downloaded
  and uploaded through the app's own cover-upload endpoint rather than
  linked externally.
- Started a "currently reading" session on `1984` (as of today), since
  that's the copy actually being read now.

### 11. Bilingual author names — the search-visibility bug, and a cleanup pass

The owner noticed "1984"'s author showed as "George Orwell" (Latin) despite
the book itself being Bulgarian, and pointed out this breaks Cyrillic
search. Investigated: `Author` is a single global entity with one
`FullName` (no `NameEn`-style alternate, unlike `Genre`), and is *shared*
across language editions of the same work — "1984" (bg), "Animal Farm"
(en), and "Фермата на животните" (bg) all pointed at the same "George
Orwell" row. Renaming it in place would've broken the English edition, so
instead: created a **separate** "Джордж Оруел" author (matching the
existing precedent already in the DB — "Руслан Трад" already existed as its
own Cyrillic entity, distinct from any Latin form) and re-pointed only the
two Bulgarian works' `WorkAuthor` links to it via `PUT /works/{id}` with
`authorNames`, leaving the English "Animal Farm" untouched.

Then swept the whole database for the same class of bug — every Bulgarian
edition whose author's name has no Cyrillic characters at all — and found
six more books. Fixed the two the owner confirmed by photo (spine text is
unambiguous ground truth): **Кристофър Хичърс** → **Кристофър Хичънс**
("God Is Not Great", bg) and **Régis Genté / Stéphane Siohan** → **Режи
Жанте / Стефан Сиоан** ("Биография на Зеленски..."). Left the other three
(a Russian/Ukrainian author's name, and two DK "Big Ideas" reference books
with six Latin-named academic contributors each) unfixed rather than guess
a transliteration that could create a wrong duplicate author — reported,
not silently resolved.

### 12. New genre: "Художествена литература" (Fiction)

Added as a genre tag (not a replacement) on "Убийство в Ориент експрес",
"1984", and "Фермата на животните" — the two Orwell books keep their
existing "Политика и история" tag alongside it, since both are genuinely
true of them.

## Why we built it this way

**A stated invariant is a rule to apply, not a suggestion to override with
"probably."** The whole Goodreads import hinged on the owner's own logic
("if a physical copy isn't in the DB, it wasn't physical") — applied
literally and consistently across 133 books, rather than trusting
Goodreads' often-unreliable `binding` field (46 "read" books were tagged
Hardcover/Paperback despite provably not being physical copies here).

**Go through the real API, not raw SQL, for anything the domain has rules
about.** The 153-row bulk import authenticated as the real user and hit
`POST /api/v1/library-items` etc., so every resolve-or-create-author,
genre, and reading-session-state check the UI itself relies on also ran
for the bulk data — raw inserts would have silently bypassed all of it.
Raw SQL was reserved for the one case the API genuinely has no path for
(changing an edition's `Format` after creation).

**Surface genuine uncertainty instead of guessing on real personal data.**
Four ambiguous Goodreads rows, nine approximate reading dates, 47
assumed-ebook formats, and three still-unfixed Latin author names were all
called out explicitly (in-chat and here) rather than silently resolved one
way — this is the owner's actual reading history and book collection, and
a wrong guess is more costly to notice and undo later than an extra
question now.

**A single global `Author`/`Work` can't represent "the same person named
differently per language" — model it as parallel entities, matching
existing precedent.** Confirmed the app already had this exact pattern
("Руслан Трад") before applying it again, rather than inventing a new
convention.

## Data changes (live, not in git — no schema change involved)

Performed via authenticated calls to the running API (session-scratch Node
scripts, not committed) plus a few narrowly-targeted direct `UPDATE`s where
no API path existed:

- 24 reading sessions backfilled on existing items; 51 new Ebook library
  items created (each with its own session); 78 new wishlist entries.
- 2 extra library items for the God Is Not Great / Hungry Ghosts
  audio-vs-physical split; 1 wishlist note edit (Incerto bundle).
- `In the Realm of Hungry Ghosts`: edition/library-item/reading-session
  `format` corrected Ebook → Audiobook directly in Postgres (no API path).
- `1984` / `Фермата на животните`: edition metadata (language, publisher,
  translator, year, ISBN, page count, cover type) corrected via
  `PUT /editions/{id}`; real cover images fetched from ciela.com and
  uploaded via `POST /editions/{id}/cover`.
- New "Джордж Оруел" author created and linked to both Bulgarian Orwell
  works; "Кристофър Хичънс" and "Режи Жанте"/"Стефан Сиоан" authors fixed
  on two more books.
- New "Художествена литература" genre created and tagged on three works.
- Final counts: **251** library items, **80** wishlist entries, reading
  sessions on every book the owner has actually finished or is reading.

## Files created/changed

- `src/MyDigitalLibrary.Application/Common/PageRequest.cs` — `MaxPageSize` 200 → 1000
- `src/MyDigitalLibrary.Application/Statistics/{StatisticsDto.cs,StatisticsService.cs}` — `BooksFinishedTotal`
- `src/MyDigitalLibrary.Application/Wishlist/{WishlistEntryDto.cs,WishlistService.cs}` — cover/language/genre on wishlist entries
- `src/web/src/app/core/api/models.ts` — `LibraryItem`/`WishlistEntry`/`StatisticsDto` field additions
- `src/web/src/app/shared/language-display.ts` — new, shared language-label helper
- `src/web/src/app/features/library/library-list/{library-list.page.ts,.html,.scss}` — pagination, sort-within-category, collapsible groups, clickable stat filters, scroll buttons, filter/sort labels, language pill
- `src/web/src/app/features/library/library-detail/{library-detail.page.ts,.html,.scss}` — mark-as-read backfill form, full edition/work metadata display, language pill
- `src/web/src/app/features/wishlist/wishlist-list/{wishlist-list.page.ts,.html,.scss}` — full redesign to the library's card-grid/grouping visuals
- `src/web/public/assets/i18n/{bg,en}.json` — new keys throughout

## What's still open

- ~40 remaining Bulgarian-edition books whose authors are DK reference-book
  contributors or a Russian/Ukrainian name — left in Latin script, pending
  the owner confirming the correct Cyrillic spelling (or saying "leave
  them," as already decided for three of them).
- Most wishlist entries have no `PreferredEditionId`, so they show no
  language tag on the wishlist page yet — cosmetic, not a data problem.
- Format (Ebook/Audiobook/Physical) is still immutable once a library item
  is created — fine for one-off fixes via direct DB access, but there's no
  UI/API path if this needs to happen again at any real frequency.
- The owner mentioned other books listened to as audio first, then bought
  physically — flagged as needing either a manual pass or a comparison
  against an Audible export, deferred until that list exists.
- The corrupted "??????" genre from a prior session is still sitting
  unused in the genres table (pre-existing, rename/delete tools exist).

## Next

Nothing queued. Waiting on the owner to keep spot-checking the imported
history and flag anything else that looks off, and to decide on the
remaining Latin-script authors and the audio-then-physical books.
