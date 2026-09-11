# Stage 0 + Stage 1 — Repo Skeleton and Domain Model

**Date:** 2026-09-11
**Stages:** 0 (repo & skeleton), 1 (domain)
**Status:** Complete
**Commits:** `4f04abd` (stage 0, prior session) · `65f5690`, `b40d472`, `8a191d1`, `8e549da`, `cc2c894` (stage 1, this session)

## What we built

**Stage 0** (done in an earlier session, summarized here for context): the solution
skeleton — `MyDigitalLibrary.slnx` referencing `Domain`, `Application`, `Infrastructure`,
`Api` under `src/`, three test projects under `tests/`, an Angular workspace under
`src/web/`, `.gitignore`, `README.md`, and a GitHub Actions CI workflow. All projects
target **.NET 10**. No docker-compose files or `.env.example` exist yet — those are
Stage 2/10 concerns, not Stage 0's.

**Stage 1**: the complete domain model from `docs/PLAN.md` section 3, with zero
dependencies beyond BCL and 61 unit tests (xUnit + FluentAssertions, no mocks).
`MyDigitalLibrary.Domain.csproj` has no `PackageReference` and no `ProjectReference` —
verified after every commit.

## Why we built it this way

### Shared kernel: `Entity`, `Result<T>`, `DomainException`
Two distinct failure channels, on purpose:
- `Result<T>` (with a stable `ErrorCode` like `isbn.invalid`) for **expected** validation
  failures on user input — the plan's API layer maps these codes to translated UI text
  later (Stage 5 i18n), so the domain must never return a human-readable message as the
  primary signal.
- `DomainException` (also carrying an `ErrorCode`) for **invariant violations** — things
  that should be impossible if calling code is correct (e.g. recording percent-progress
  against an audiobook session).

`Entity.Id` uses `protected init` rather than a constructor parameter, so it stays
flexible for Stage 2 (EF Core) without adding a second, currently-unused constructor now.

### `Edition` grew a `Format` field the plan's prose didn't show in code
Plan section 3.1 says a `LibraryItem` is "one `Edition` in a particular `Format`," which
reads as if `Format` belongs only to `LibraryItem`. But `Edition`'s own fields are
format-specific (page count + cover type vs. narrator + duration), and the worked example
— the same *Dune* legitimately owned as paperback + ebook + audiobook — implies three
different `Edition`s, not one `Edition` reused across formats. So `Edition.Format` is now
the authoritative field, and `SetPublicationDetails` / `SetCoverType` / `SetAudioDetails`
throw a `DomainException` if you set a format-specific field that doesn't match.
`LibraryItem.Format` stays too (as in the plan's snippet) — a deliberate denormalized copy,
same reasoning as below.

### `ReadingSession.Format` — a deliberate denormalized field
Plan section 3.8 is explicit: aggregates reference each other **only by Guid**, never by
navigation property. But `ReadingSession.RecordProgress` must reject a `PercentProgress`
against an audiobook — and it can't ask the `LibraryItem`/`Edition` aggregate what format
it is without a navigation property or a repository call, neither of which belongs in a
domain method. The resolution: `ReadingSession` captures `Format` once, at construction,
as its own field. This is the only place in the domain that duplicates data across an
aggregate boundary, and it exists specifically because the alternative (skipping the
invariant, or querying across aggregates from inside a domain method) was worse than a
one-line comment explaining why the copy exists.

### `WorkRating` scale: 1–10
Plan section 14 (open questions) left the rating scale undecided, deferring to "before
Stage 3." Since the invariant (`MinScore`/`MaxScore` range check) had to be written now,
the owner was asked directly rather than guessed: **1–10** was chosen over 1–5 stars.

### `ManualFieldOverrides` was deliberately NOT built in Stage 1
It appears in plan section 5.5 ("manual edits always win"), which is Stage 6 (import)
scope, not section 3 (domain model, Stage 1 scope). Building it now would have meant
guessing at an enrichment policy before the import pipeline that uses it exists.

### `BookstoreListing.RecordFailedCheck` never touches `Availability`
Plan section 6.2 is explicit: "never record OutOfStock if the scrape failed." The method
only increments `ConsecutiveFailures`; a separate `MarkUnknownDueToRepeatedFailures()`
exists for the caller (Stage 8's `AvailabilityRefreshService`) to invoke once it decides
enough consecutive failures have accumulated — the *threshold* (`N`) is deliberately left
as that future caller's decision, not hard-coded into the entity now.

### `ReadingSession.Abandon(on, reason)` — the plan's signature had an orphaned parameter
Section 3.5's code snippet declares `Abandon(DateOnly on, string? reason)` but never shows
a field to store `reason` in. Since discarding an explicitly-passed parameter silently
seemed like a bug waiting to happen, `AbandonReason` was added as a property.

## Files created (Stage 1)

### Common (`src/MyDigitalLibrary.Domain/Common/`)
`Entity.cs`, `Result.cs`, `DomainException.cs`

### Enums (`src/MyDigitalLibrary.Domain/Enums/`)
`BookFormat`, `CoverType`, `OwnershipStatus`, `ReadingStatus`, `MarketAvailability` (from
plan 3.2) + `AcquisitionMethod`, `WorkAuthorRole`, `ImportJobKind`, `ImportJobStatus`
(needed by 3.4/3.7 but not spelled out as enums in 3.2)

### Value objects (`src/MyDigitalLibrary.Domain/ValueObjects/`)
`Isbn.cs` (full ISBN-10/13 checksum validation, ISBN-10 → ISBN-13 normalization),
`Money.cs`, `AudioDuration.cs`, `SeriesPosition.cs`, `PhysicalLocation.cs`,
`Acquisition.cs`

### Reading (`src/MyDigitalLibrary.Domain/Reading/`)
`ReadingSession.cs` (aggregate root), `ProgressPoint.cs` (abstract) with
`PageProgress.cs` / `PercentProgress.cs` / `TimestampProgress.cs`, `ProgressEntry.cs`
(child entity), `EditionExtent.cs` (`PageCountExtent` / `DurationExtent`)

### Catalog — shared, no `UserId` (`src/MyDigitalLibrary.Domain/Catalog/`)
`Work.cs`, `WorkAuthor.cs`, `Edition.cs`, `Author.cs`, `Series.cs`, `Genre.cs`,
`Bookstore.cs`, `BookstoreListing.cs`, `PriceHistoryEntry.cs`

### Library — per-user (`src/MyDigitalLibrary.Domain/Library/`)
`LibraryItem.cs`, `WishlistEntry.cs`, `Tag.cs`, `Shelf.cs`, `ShelfItem.cs`, `Note.cs`,
`Quote.cs`, `Loan.cs`, `ReadingGoal.cs`, `WorkRating.cs`, `Review.cs`, `ImportJob.cs`

### Tests (`tests/MyDigitalLibrary.Domain.Tests/`, mirrors the folder layout above)
61 tests total across `ValueObjects/`, `Reading/`, `Catalog/`, `Library/`. Added
`FluentAssertions` (8.10.0) as a package reference; removed the scaffolded
`UnitTest1.cs` placeholder.

## Key domain rules encoded

| Rule | Where |
|---|---|
| `PercentProgress` rejected for an audiobook session | `ReadingSession.RecordProgress` → `reading_session.progress_format_mismatch` |
| Rereading creates a new `ReadingSession`, never overwrites the old one | Just a `new ReadingSession(...)` call — history lives because nothing ever mutates a past session's identity |
| `WishlistEntry.Fulfill()` creates a `LibraryItem` with the given `Acquisition` and closes the wish (once) | `WishlistEntry.cs` → `wishlist_entry.already_fulfilled` on a second call |
| Invalid ISBN-10/13 checksum rejected without throwing | `Isbn.TryCreate` → `Result<Isbn>.Failure("isbn.invalid", ...)` |
| A physical `LibraryItem` may have a `PhysicalLocation`; a non-physical one may not | `LibraryItem.SetLocation` → `library_item.location_requires_physical` |
| `Edition` page count / cover type / narrator+duration are format-gated | `Edition.cs` → `edition.page_count_requires_print_or_ebook`, `edition.cover_type_requires_physical`, `edition.audio_details_require_audiobook` |
| A scrape failure never flips a listing to `OutOfStock` | `BookstoreListing.RecordFailedCheck` only increments a counter; availability is untouched |
| One `WorkRating` per `(UserId, WorkId)`, scored 1–10 | Uniqueness is a Stage 2 DB index; range is `WorkRating.MinScore`/`MaxScore` |
| A system `Shelf` (e.g. "Finished") cannot be renamed | `Shelf.Rename` → `shelf.system_shelf_immutable` |

## What's still open

- **User isolation test** (plan section 11, test #5 — "user A cannot see user B's
  `LibraryItem`") needs `ICurrentUser` and EF global query filters — Stage 4.
- **"Not persisted" half of the invalid-ISBN test** (test #4) needs the API/DB round
  trip — Stage 3.
- **`ManualFieldOverrides` + re-enrichment survival test** (test #6) — Stage 6, as
  planned.
- Docker Compose, `.env.example`, and the `docs/adr/` ADR records are still empty —
  none of Stage 1's scope required them, but Stage 2 (persistence) will need
  `docker compose up db api` to work per the plan's stage-completion rule.

## Next

Stage 2 — Persistence: `DbContext`, `IEntityTypeConfiguration<T>` per entity in
`Infrastructure/Persistence/Configurations/`, first EF Core migration, seed data
(2 works, 3 editions, 2 library items), `docker compose up db api`.
