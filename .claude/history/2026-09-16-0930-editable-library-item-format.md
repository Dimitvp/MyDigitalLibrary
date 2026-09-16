# Editable library item format (physical/ebook/audiobook correction)

**Date:** 2026-09-16
**Context:** On the library item detail page, the owner spotted "The Origins of
Political Order" (Fukuyama) recorded as Format = Електронна (Ebook) even
though its own personal note read "Narrated by Jonathan Davis Audio book" —
clearly an audiobook, mislabeled. There was no way to correct it: Format was
rendered as a plain read-only label. The owner connected this to a broader gap
— many books imported from Goodreads were read/listened to in a format the
import guessed wrong — and asked for a dropdown (paper / electronic / audio,
the only three formats a book can be in) to fix it in place.
**Status:** Complete. 243/243 backend tests green (7 new), rebuilt and
verified end-to-end against the live docker stack — including fixing the
Fukuyama book itself, which was the trigger for this.
**Commits:** this session's commit (see `git log`).

## Root cause

`Edition.Format` and `LibraryItem.Format` (`BookFormat`: Physical/Ebook/
Audiobook) were both `{ get; }` — set once in the constructor, no mutator
existed on either entity. Every *other* edition/item field has a `SetX`/
`ChangeX` method (`SetIsbn`, `SetCoverType`, `ChangeStatus`, ...); Format was
the one field nothing could revise after creation. Confirmed via the
`GoodreadsCsvParser`: it maps the CSV "Binding" column by substring match
(`kindle`/`ebook`/`nook`→Ebook, `audio`→Audiobook) and **defaults to
`BookFormat.Physical` whenever Binding is blank or unrecognized** — exactly
the kind of guess the owner suspected was silently wrong for a chunk of the
imported library.

## What changed

**Domain** (`ChangeFormat` added to both, mirroring the existing throwing-
setter pattern rather than just flipping the enum):
- `Edition.ChangeFormat(BookFormat)` — clears whatever fields the *existing*
  throwing setters would now reject for the new format: ISBN and page count
  when switching to Audiobook, cover type whenever leaving Physical
  (reset to `CoverType.Unknown`), narrator/duration whenever leaving
  Audiobook. A same-format call is a no-op (doesn't clear anything).
- `LibraryItem.ChangeFormat(BookFormat)` — drops `Location` when leaving
  Physical, matching `SetLocation`'s own physical-only validation.
- Both `Format` properties changed from `{ get; }` to `{ get; private set; }`.
  No migration needed — same column, just a C# accessibility change.

**Application/API:**
- `LibraryItemService.ChangeFormatAsync(id, format, userId, ct)` — loads the
  item (user-scoped) and its edition, calls `ChangeFormat` on both so they
  never drift out of sync (the DTO already documented Format as
  "comes from the request's top-level Format, not repeated" — i.e. these two
  copies of the same fact were always meant to move together).
- `UpdateFormatRequest(BookFormat Format)` DTO; `PATCH /api/v1/library-items/
  {id}/format`, same shape as the existing `/status` and `/location` PATCH
  endpoints in `LibraryItemEndpoints.cs`.

**Angular:**
- `LibraryApiService.updateFormat(id, format)` — PATCH, mirrors `updateStatus`.
- `library-detail.page.html`: the read-only `<dd>{{ 'library.format.' +
  item.format | transloco }}</dd>` became a `<select>` bound to
  `item.format`, firing `onFormatChange` on `(change)`. No i18n keys needed —
  `library.format.{Physical,Ebook,Audiobook}` already existed.
- `library-detail.page.ts`: `onFormatChange` PATCHes, then calls
  `itemResource.reload()` and `editionResource.reload()` (existing
  `.reload()` pattern, used elsewhere for the genre picker and wishlist
  pages) instead of hand-mirroring every field the format change might have
  cleared server-side (ISBN/page count/cover type/narrator/duration) — the
  reload just re-fetches the now-correct state.

**Tests** (94→98 Domain, 131→132 API integration; 243 total):
- `EditionTests`: format-change clears ISBN+page count (→Audiobook),
  narrator+duration (away from Audiobook), cover type (away from Physical);
  same-format call is a no-op.
- `LibraryItemTests`: format-change clears location (away from Physical);
  same-format call is a no-op.
- `LibraryItemEndpointsTests`: `PATCH /format` updates both the item and its
  edition, and clears the now-invalid ISBN — full round trip through the API.

## Verified live

Rebuilt `api` and `web` (`docker compose up -d --build api web`); API started
with "No migrations were applied. The database is already up to date." as
expected. Logged in as the seed admin against the running stack and PATCHed
the actual Fukuyama item that triggered this — confirmed via `GET`:
`format: "Audiobook"`, and its edition's `isbn13`/`pageCount`/`coverType` all
cleared, `publisher`/`publicationYear` (format-agnostic fields) left alone.
This was the owner's real data, not a throwaway — the fix doubled as the
actual correction they asked for.

## Why we built it this way

**Correcting a field needs the same discipline as setting it the first
time.** Every existing `SetX` method on `Edition` throws on a
format-mismatched value rather than silently accepting bad state (`SetIsbn`
on an audiobook, `SetCoverType` on an ebook, etc.). A naive `ChangeFormat`
that just flipped the enum would have left those methods' invariants
violated the moment you changed format out from under already-set fields —
an audiobook edition still carrying a page count and an ISBN. Clearing the
now-invalid fields inside `ChangeFormat` itself keeps the entity's own
invariants intact without needing the caller (the API layer, or any future
caller) to remember to clean up after itself.

**Two copies of one fact, kept explicitly in sync, not merged into one.**
`Edition.Format` and `LibraryItem.Format` are redundant by design — the
`LibraryItemDto` comment already flagged this ("Format comes from the
request's top-level Format, not repeated here"). Rather than removing the
duplication (a bigger, unrelated change touching every query that filters
`LibraryItem.Format` directly to skip an edition join), `ChangeFormatAsync`
just updates both in the same transaction, the same way creation already
does.

## Files changed

- `src/MyDigitalLibrary.Domain/Catalog/Edition.cs` — `ChangeFormat`, `Format` setter
- `src/MyDigitalLibrary.Domain/Library/LibraryItem.cs` — `ChangeFormat`, `Format` setter
- `src/MyDigitalLibrary.Application/LibraryItems/LibraryItemDto.cs` — `UpdateFormatRequest`
- `src/MyDigitalLibrary.Application/LibraryItems/LibraryItemService.cs` — `ChangeFormatAsync`
- `src/MyDigitalLibrary.Api/Endpoints/LibraryItemEndpoints.cs` — `PATCH /{id}/format`
- `src/web/src/app/features/library/library-api.service.ts` — `updateFormat`
- `src/web/src/app/features/library/library-detail/library-detail.page.{ts,html,scss}` —
  format `<select>`, `onFormatChange`, `.format-select` styling
- `tests/MyDigitalLibrary.Domain.Tests/Catalog/EditionTests.cs`,
  `tests/MyDigitalLibrary.Domain.Tests/Library/LibraryItemTests.cs`,
  `tests/MyDigitalLibrary.Api.IntegrationTests/LibraryItems/LibraryItemEndpointsTests.cs` — new tests

## What's still open

The owner mentioned many Goodreads-imported books likely have the wrong
format (importer defaults to Physical when "Binding" is blank/unrecognized).
This session only builds the correction tool (the dropdown); going through
the imported library and fixing individual mislabeled items is follow-up
work the owner didn't ask to start yet.
