# Language dropdown, ISBN search, edition-conflict messaging, audiobook ISBN rule, publication-year labels

**Date:** 2026-09-13
**Context:** A grab-bag of five small owner-reported issues found while using
the live app, each raised as a screenshot mid-session rather than planned
work: a free-text language field that let a typo fork a book into its own
group on the library list, a wish to search by ISBN, a confusing "duplicate
ISBN" error while editing a book, a question about why an audiobook and a
paperback both refused to share/keep an ISBN, and two identically-labeled
"Година на издаване" fields on the edit page with no way to tell which was
which.
**Status:** Complete. All five fixed and verified (backend: `dotnet build` +
targeted `dotnet test` runs, all green; frontend: `tsc --noEmit` clean, JSON
locale files validated). Not yet committed — see "Next".

## 1. Library-edit language field: free text → dropdown

**Symptom:** The add-book form's language field was already a `bg`/`en`
`<select>`, but the *edit* page's edition-section language field
(`library-edit.page.html`) was a plain `<input type="text">`. Typing
"български" instead of picking `bg` created a value the library list's
grouping (`library-list.page.ts`, keyed by the raw `item.language` string)
treated as a distinct group from `bg` — literal duplicate "Български" /
"БЪЛГАРСКИ" section headers on the list page for what was otherwise the same
language.

**Fix:**
- `library-edit.page.html` — edition-section language field changed from
  `<input>` to the same `<select>`/`bg`/`en` markup already used by the add
  form.
- `library-edit.page.ts` — the edition-load effect now normalizes any stored
  value to `'bg'` unless it's exactly `'en'` (`edition.language === 'en' ?
  'en' : 'bg'`), instead of passing whatever free-text string was stored
  straight into the form.

No backend change — `Edition.Language` was always an unconstrained
`string?`; this was purely a UI gap between the two forms.

## 2. ISBN search on the library list

**Ask:** "искам в търсачката да мога да търся по ISBN" — the library list's
search box (`q` param, currently matching title word-prefixes and author
names via Postgres regex) didn't match ISBN at all.

**Backend (`LibraryItemService.ListAsync`):** `Edition.Isbn13` is a scalar
`HasConversion` type (see the existing comment in `ExportService.cs`) — its
`.Value` can't be unwrapped inside a translated `Where()`/`Select()`, so
substring/regex matching can't be pushed into the same correlated subquery
used for title/author. Added a small pre-pass instead: strip all non-digit
characters from `q`; if 3+ digits remain, fetch `{Id, Isbn13}` for every
edition with a non-null ISBN (bounded, personal-library-sized set),
`.Contains(digits)` in memory, and fold the matching edition ids into the
existing `Where()` as an `isbnEditionIds.Contains(li.EditionId)` OR-branch.
Guarded behind the 3-digit minimum so plain text searches never pay for the
extra round trip.
- New integration test `Listing_matches_q_against_isbn` — full ISBN with
  dashes (digit-stripping) and a 6-digit suffix (substring match).

**Frontend:** search placeholder updated in both locales — "Търсене по
заглавие..." → "Търсене по заглавие, автор или ISBN..." (and the English
equivalent) — since it now genuinely searches all three.

## 3. Duplicate-ISBN error names the conflicting book

**Trail:** while testing #2, the owner hit "има книга с този номер" while
editing a book's ISBN and — reasonably, since the message didn't say which
book — assumed it must be the audiobook edition of the same title sitting
next to it. A live-DB check (`docker exec mydigitallibrary-db-1 psql`)
showed the actual conflict was a *different*, pre-existing Work ("Бог не е
велик: Как религията трови света", a separate unlinked Bulgarian-language
Work from the English "God Is Not Great" audiobook Work) — a genuine
duplicate the owner then found and deleted themselves. The underlying
uniqueness check was working correctly; the message just didn't say who it
was conflicting with.

**Fix:** `BookCatalogService.CreateEditionAsync` and
`EditionService.UpdateAsync` — the `ConflictException` thrown on a duplicate
ISBN now looks up the existing edition's `Work.Title` and includes it as an
`existingWorkTitle` extension (alongside the pre-existing `existingEditionId`
and a new `existingWorkId`), and the exception message itself now reads
`"...already exists ({existingWorkTitle})."` instead of the bare ISBN.

**Frontend plumbing to surface it:**
- `NotificationService.show()` gained an optional `params` argument;
  `Notification` gained `params?: Record<string, unknown>`.
- `notification.ts` template passes `notification.params` through to the
  `transloco` pipe (`{{ ... | transloco: notification.params }}`), matching
  the interpolation pattern already used elsewhere (e.g.
  `wishlist-list.page.html`'s `findCoversProgress`).
- `error.interceptor.ts` — when the ProblemDetails body carries a string
  `existingWorkTitle`, it's passed through as `{ title }`.
- `bg.json`/`en.json` — `errors.edition.duplicate` now reads "Вече има
  издание с този ISBN — виж „{{title}}"." / `An edition with this ISBN
  already exists — see "{{title}}".`

## 4. Audiobooks can no longer be given an ISBN

**Root cause, found while investigating #3:** the Goodreads/Calibre CSV
importer (`CsvImportService.ImportRowAsync`) called `edition.SetIsbn(isbn)`
unconditionally for every new edition regardless of format — so an
audiobook row carrying its print edition's ISBN (common in Goodreads
exports) would land in the DB with that ISBN attached, silently able to
collide with the real print edition later. A live-DB check found exactly
one such row already: "In the Realm of Hungry Ghosts" (Audiobook,
`isbn13 = 9780676977400`), cleared directly via `UPDATE editions SET isbn13
= NULL ...` (single row, non-destructive, no other edition held that ISBN
at the time).

**Ask confirmed by the owner:** "аудио книги не трябва да имат такъв номер
... иначе ми харесва идеята този номер да е уникален" — keep the uniqueness
constraint, just stop it from applying to audiobooks at all.

**Fix, mirroring the existing per-format validation pattern** (page count
already throws for Audiobook, narrator/duration already throw for
non-Audiobook, cover type already throws for non-Physical):
- `Edition.SetIsbn` (`Domain/Catalog/Edition.cs`) now throws
  `DomainException("edition.isbn_requires_print_or_ebook", ...)` if a
  non-null ISBN is set on a `BookFormat.Audiobook` edition. Maps to a 409 via
  the existing `ApplicationExceptionHandler` `DomainException` case — no API
  handler changes needed.
- `CsvImportService.ImportRowAsync` — `edition.SetIsbn(row.Format ==
  BookFormat.Audiobook ? null : isbn)`, same guard style already used one
  line below for `PageCount`.
- `library-edit.page.html` — ISBN field wrapped in `@if (format() !==
  'Audiobook')`, matching the existing conditional style for pageCount/
  coverType/narrator in that same form.
- `library-form.page.ts`/`.html` (the add form) — added
  `selectedFormat = toSignal(this.form.controls.format.valueChanges, ...)`
  since this form had no existing signal mirroring the format control's
  live value; ISBN field wrapped in the same `@if`.
- Both `saveEdition()` (library-edit) and `submit()` (library-form) now
  force `isbn13: null` in the outgoing request whenever the format is
  Audiobook, as a belt-and-suspenders guard against a stale/hidden-field
  value ever reaching the API even if the template conditional were bypassed.
- `bg.json`/`en.json` — new `errors.edition.isbn_requires_print_or_ebook` key.
- New domain tests: `SetIsbn_rejects_an_isbn_on_an_audiobook`,
  `SetIsbn_allows_clearing_an_audiobooks_isbn`.

## 5. Publication-year labels disambiguated

**Symptom:** the library-edit page has two "Година на издаване" fields —
the Work section's `firstPublicationYear` (the original work's first-ever
publication year, language-agnostic) and the Edition section's
`publicationYear` (this specific printing/translation's year) — both using
the same `library.form.publicationYear` translation key, with nothing on
screen distinguishing them. Purely a labeling gap (the two values were
already stored and submitted separately) — no backend change needed.

**Fix:**
- New keys `library.form.firstPublicationYear` ("Година на първото издание
  (оригинал)" / "First publication year (original)") and
  `library.form.editionPublicationYear` ("Година на издаване на това
  издание" / "This edition's publication year") added to both locales.
- `library-edit.page.html` — the Work-section field now uses
  `firstPublicationYear`, the Edition-section field now uses
  `editionPublicationYear`. The add form and detail page were left on the
  original generic `publicationYear` key since neither shows both years at
  once.

## 6. (Informational, no code change) ISBN-10 vs ISBN-13

Separately, the owner asked why typing an ISBN-10 (`0756689015`) got
silently rewritten to `9780756689018` on save. Explained: this is
`Isbn.TryCreate`'s documented, intentional behavior ("always normalized and
stored as ISBN-13") — the two numbers identify the same edition, ISBN-13 is
just the post-2007 industry-standard representation, and the converted value
matches exactly what Amazon's own "ISBN-13" field showed. No bug, nothing
changed.

## Why we built it this way

**Format-specific field rules belong on the entity, not scattered across
forms.** `SetIsbn`'s new throw follows the exact shape already established
by `SetPublicationDetails` (page count) and `SetAudioDetails`
(narrator/duration) — one place enforces "this field doesn't apply to that
format," and every caller (manual edit, composite create, CSV import)
inherits it for free instead of needing its own check.

**A conflict error should name the conflict.** `existingEditionId` alone
(the pre-existing behavior) is enough for an API client but useless to a
person reading a toast — adding `existingWorkTitle` cost one extra query and
turned "something already has this ISBN, good luck finding it" into an
actionable message.

**Fix the data you find while you're already looking.** The one stray
audiobook ISBN found via the live-DB check was cleared immediately rather
than left for a future collision, since the whole point of the session's
ask was to stop that exact class of bug from recurring.

## Files changed

- `src/MyDigitalLibrary.Domain/Catalog/Edition.cs` — `SetIsbn` format guard
- `src/MyDigitalLibrary.Application/Catalog/BookCatalogService.cs` —
  duplicate-ISBN conflict now includes `existingWorkId`/`existingWorkTitle`
- `src/MyDigitalLibrary.Application/Editions/EditionService.cs` — same, for
  the edition-update path
- `src/MyDigitalLibrary.Application/Import/Csv/CsvImportService.cs` — skips
  ISBN for Audiobook rows
- `src/MyDigitalLibrary.Application/LibraryItems/LibraryItemService.cs` —
  ISBN search branch in `ListAsync`
- `tests/MyDigitalLibrary.Domain.Tests/Catalog/EditionTests.cs` — two new
  `SetIsbn` tests
- `tests/MyDigitalLibrary.Api.IntegrationTests/LibraryItems/LibraryItemEndpointsTests.cs`
  — `Listing_matches_q_against_isbn`, `CreateLibraryItemAsync` helper gained
  an `isbn13` parameter
- `src/web/src/app/features/library/library-edit/library-edit.page.html` —
  language `<select>`, ISBN field hidden for Audiobook, publication-year
  label split
- `src/web/src/app/features/library/library-edit/library-edit.page.ts` —
  language normalization on load, `isbn13: null` guard on save
- `src/web/src/app/features/library/library-form/library-form.page.html` —
  ISBN field hidden for Audiobook
- `src/web/src/app/features/library/library-form/library-form.page.ts` —
  `selectedFormat` signal, `isbn13: null` guard on submit
- `src/web/src/app/shared/ui/notification/notification.service.ts` —
  `params` support
- `src/web/src/app/shared/ui/notification/notification.ts` — passes `params`
  to the `transloco` pipe
- `src/web/src/app/core/http/error.interceptor.ts` — forwards
  `existingWorkTitle` as a notification param
- `src/web/public/assets/i18n/bg.json`, `en.json` — search placeholder,
  `errors.edition.duplicate` interpolation, new
  `errors.edition.isbn_requires_print_or_ebook`,
  `library.form.firstPublicationYear`, `library.form.editionPublicationYear`

## Data fixed live (not a migration — direct one-off corrections)

- Cleared `isbn13` on the one existing Audiobook edition that had one
  ("In the Realm of Hungry Ghosts", `9780676977400`) via a single `UPDATE`
  against the running `mydigitallibrary-db-1` container.
- The genuine duplicate Work behind the "God Is Not Great" ISBN conflict
  (the separate, unlinked "Бог не е велик..." Work) was found and deleted by
  the owner themselves through the app once the conflict message pointed at
  it.

## Verified

- `dotnet build` clean across the solution after every backend change.
- `dotnet test` on the affected suites, all green:
  - `MyDigitalLibrary.Domain.Tests` (`EditionTests`) — 11/11
  - `MyDigitalLibrary.Api.IntegrationTests` filtered to
    `Import|LibraryItem|Edition` — 57/57
  - `MyDigitalLibrary.Api.IntegrationTests` filtered to
    `LibraryItemEndpointsTests` alone — 8/8 (includes the new ISBN-search
    test)
- `npx tsc --noEmit -p tsconfig.app.json` clean after every frontend change.
- `bg.json`/`en.json` validated as well-formed JSON after each edit.
- Live-DB checks via `docker exec mydigitallibrary-db-1 psql` used to
  diagnose both the ISBN-conflict and stray-audiobook-ISBN issues before
  writing any fix.

## Next

Not yet committed or pushed — pending owner confirmation of the commit
message/push (see the immediately following ask in this session).
