# Wishlist already-owned guard (title + ISBN, language-aware), ISBN/language now persist on add, new wishlist detail page

**Date:** 2026-09-14
**Context:** Three separate asks in one sitting, each a follow-up to the
last. First, the owner wanted adding a book to "Искам я" (wishlist) to be
blocked when that book is already in "Имам я" (library), checked by title
and ISBN. Second, after trying it, they noticed the ISBN/language they'd
just typed on the add form vanished — the edit page came up blank and they
had to retype both — so the values needed to actually persist, not just
feed the duplicate check and get thrown away. Third, working from a
screenshot of a library book's detail page, they asked for the same
single-item "open and see everything" view for each wishlist entry, which
today only has a list card and a heavy edit form, no read-only detail page.
**Status:** Complete. Backend logic verified with real HTTP calls against
the rebuilt dev API container (login → antiforgery → four duplicate-check
scenarios → ISBN/language persistence check), all test data cleaned up
from the dev database afterward (including orphaned `Work`/`Edition`/
`Author` rows the delete endpoints don't cascade). Frontend verified with
a scripted Playwright run against the owner's own already-running `ng
serve` (my own attempt to start a second instance on the same port failed
immediately with EADDRINUSE and its output was never used — the dev
server that answered every request was the owner's, which picked up the
new files via its own file watcher). `tsc --noEmit` and `ng build
--configuration development` both clean; backend `dotnet build` clean.
**Commits:** none yet — this history entry is being written and pushed
together with the diff in the same request.

## What we built

### 1. Block wishlisting a book already in the library, by title and ISBN

`WishlistService.CreateAsync` (`src/MyDigitalLibrary.Application/Wishlist/
WishlistService.cs`) now calls a new `EnsureNotAlreadyOwnedAsync` before
constructing the `WishlistEntry`, which joins the caller's `LibraryItems →
Editions → Works` (excluding `Sold`/`GivenAway` items — the owner no
longer has those, so wanting one again is legitimate) and checks:

- **Exact ISBN match** → always blocks, via `ConflictException
  ("wishlist_entry.already_owned", ...)` (409), same exception shape as
  the existing `edition.duplicate` check in `BookCatalogService.
  CreateEditionAsync`.
- **Title match** (trimmed, case-insensitive) → blocks too, *unless* the
  caller supplied a `language` that is known to differ from every owned
  copy's language. This mirrors an explicit lesson from an earlier session
  ([[feedback-libristo-import]]): owning a book in Bulgarian doesn't make
  wanting the English edition a duplicate — but that exception only
  applies when both languages are actually known, not merely absent.

This needed a design decision the owner was asked about directly (via
`AskUserQuestion`), since the wishlist add form collects no ISBN or
language today: they chose "add an ISBN field + also check language" over
the simpler "always block on title match regardless of language" or
"block but let the user confirm anyway" options. `CreateWishlistEntryRequest`
gained two new optional fields, `Isbn13`/`Language`, used only to run this
check (not persisted as-is — see next section for what happens to them).

### 2. ISBN/language typed on the add form now actually persist

Originally `Isbn13`/`Language` were write-only inputs to the duplicate
check and then discarded — so after saving, the wishlist entry had no
edition, and the edit page showed both fields empty, forcing a retype.
The owner caught this from a screenshot of the empty edit-page fields
right after adding a book.

Fixed by having `CreateAsync` create a real `Edition` when either field is
present and no `PreferredEditionId` was already given — reusing
`BookCatalogService.CreateEditionAsync` (same catalog-wide duplicate-ISBN
guard as edits made later) and setting the new edition as the entry's
`PreferredEditionId`. Everything lands in the one `SaveChangesAsync` the
method already had. Verified end-to-end: `POST /wishlist` with a valid
ISBN+language now returns a non-null `preferredEditionId`, and `GET
/editions/{id}` shows both fields exactly as submitted.

Frontend: `wishlist-form.page.ts`/`.html` gained two optional fields
(ISBN text input, bg/en language select, mirroring the pattern already
used on `wishlist-edit.page.html`'s edition section) and
`models.ts`'s `CreateWishlistEntryRequest` gained the matching
`isbn13?`/`language?` fields.

New translation key: `wishlist_entry.already_owned` in both `bg.json`
("Вече имаш „{{title}}" в библиотеката си.") and `en.json` — picked up
automatically by the existing global `error.interceptor.ts`, no frontend
error-handling code needed (same mechanism as `edition.duplicate`).

### 3. A wishlist detail page, matching Library's single-item view

New route `/wishlist/:id` (`wishlist-detail/wishlist-detail.page.{ts,html,
scss}`, registered last in `wishlist.routes.ts` after the more specific
`:id/edit`/`:id/fulfill` segments — same ordering convention
`library.routes.ts` already uses). Built by copying `library-detail.page.
*`'s markup/CSS wholesale (cover, title/author, pill row, `<dl>` info
table, note textarea + save) and stripping what a `WishlistEntry`
structurally can't have: no reading-session block (wishes have no
`ReadingSession` concept at all — confirmed by reading `WishlistEntry.cs`'s
own doc comment, which states the separate-aggregate design intent
explicitly), no ownership-status pill, no acquisition fields — replaced
with what a wish *does* carry instead: priority, out-of-stock flag, added-
on date, max price.

The owner was asked (via `AskUserQuestion`) whether the new page should
try to approximate reading/ownership info anyway; they picked the
"information + note only" option, confirming the structural gap was
intentional rather than something to paper over.

`wishlist-list.page.html`'s card markup changed so the cover+title+info
block is now wrapped in `<a [routerLink]="['/wishlist', entry.id]"
class="card-link">`, matching how `library-list.page.html` already wraps
its whole card in a single link — the three existing action buttons
(edit/fulfill/delete) moved to a sibling `<span class="card-actions">`
outside that anchor rather than nested inside it (an anchor can't validly
contain `<button>`/nested `<a>`). `wishlist-list.page.scss` got a new
`.card-link` flex rule to keep the card's internal layout (cover, then
info growing to push meta/note/added-date to the bottom) identical to
before, and `.card-actions` picked up the horizontal/bottom padding it
used to inherit for free from being inside `.info`.

## Why we built it this way

**A duplicate check needed a real design call, not a guess, because it
directly touches a rule the owner had already stated explicitly in a past
session.** [[feedback-libristo-import]] records "внимавай с дубликатите,
може някоя книга да я имам на бълг. но да я искам на англ." as a
correction given mid-task, not a hypothetical — implementing today's
"check by title and ISBN" literally, with no language exception, would
have silently regressed that rule the next time the owner wanted a
different-language edition of something they own. Asked rather than
assumed, since the two readings (strict block vs. language-aware
exception) produce materially different, hard-to-notice behavior.

**Verify against the real dev stack, not a mental model of the code.**
Both the backend duplicate/persist logic and the frontend detail page were
checked by actually rebuilding the Docker API image and running real HTTP
requests (with real login/antiforgery), and by driving the owner's live
`ng serve` with Playwright, rather than trusting a clean compile alone —
which is also how the ISBN-checksum mistake in the first test payload and
the AcquisitionMethod enum-value mistake in the test library item got
caught immediately instead of shipping unverified.

**Test data in someone's personal, real database must be cleaned up
completely, including what the API can't delete for you.** Deleting a
`LibraryItem`/`WishlistEntry` through the API doesn't cascade to the
`Work`/`Edition`/`Author` rows it created — those were removed by hand via
direct SQL against the dev Postgres container afterward, cross-checked by
diffing the orphan-author list before/after so only the rows this session
created were touched, not the pre-existing unrelated orphans already in
the owner's data (e.g. `Isaac Asimov`, `Frank Herbert`).

**Don't touch a process you didn't start.** The port-4201 dev server was
already running (visible in the owner's own screenshot before this
session touched anything); my own `ng serve --port 4201` failed to bind
and its log was checked before assuming otherwise, so the already-running
process was left alone rather than killed to "clean up."

## Files created/changed

- `src/MyDigitalLibrary.Application/Wishlist/WishlistEntryDto.cs` —
  `CreateWishlistEntryRequest` gained optional `Isbn13`/`Language`.
- `src/MyDigitalLibrary.Application/Wishlist/WishlistService.cs` —
  `CreateAsync` now resolves the work's title before constructing the
  entry, calls `EnsureNotAlreadyOwnedAsync` (new private method), and
  lazily creates a `PreferredEditionId` edition when ISBN/language were
  supplied; `AlreadyOwned` helper builds the `ConflictException`.
- `src/web/src/app/core/api/models.ts` — `CreateWishlistEntryRequest`
  gained optional `isbn13`/`language`.
- `src/web/src/app/features/wishlist/wishlist-form/wishlist-form.page.ts`/
  `.html` — new ISBN text input + bg/en language select on the add form.
- `src/web/src/app/features/wishlist/wishlist-list/wishlist-list.page.html`/
  `.scss` — card's cover+info wrapped in a `routerLink` to the new detail
  page; actions moved to a sibling block outside that link.
- `src/web/src/app/features/wishlist/wishlist.routes.ts` — new `:id`
  route, appended after `:id/edit`/`:id/fulfill`.
- `src/web/src/app/features/wishlist/wishlist-detail/wishlist-detail.page.
  {ts,html,scss}` (new) — read-only detail view: cover, title/author, pill
  row (language/cover type/priority/out-of-stock), info table (format,
  ISBN/publisher/year/pages/translator/narrator/duration when an edition
  exists, added-on date, max price), note + save, edit/fulfill/delete
  actions.
- `src/web/public/assets/i18n/bg.json`, `en.json` — new
  `wishlist_entry.already_owned` key; every other label on the new page
  reused existing `library.form.*`/`library.detail.*`/`wishlist.list.*`
  keys, so no other new strings.

## What's still open

- The duplicate check only fires on `CreateAsync` (adding a new wish).
  Changing a wishlist entry's title/edition later via the edit page (or
  attaching an edition after the fact) doesn't re-run this check — not
  something the owner asked for, flagged here in case it matters later.
- No automated tests were added — `MyDigitalLibrary.Application.Tests`
  has zero existing test files to follow a pattern from, and standing up
  EF Core test infrastructure from scratch was judged out of scope for
  this request; verification instead relied on real HTTP calls against
  the dev stack (see Status above).

## Next

Nothing queued. Waiting on the owner to try adding a genuinely
already-owned book (by title and by ISBN) through the real UI, and to
open a few wishlist entries' new detail pages in normal use.
