# Stage 2 — Persistence

**Date:** 2026-09-11
**Stage:** 2 (persistence)
**Status:** Complete
**Commits:** `d3a1a48`, `1c9d9eb`, `f35df80`, `a1ea06b`, `d89e182`, `922e011`

## What we built

EF Core + PostgreSQL persistence for the full Stage 1 domain model: `MyDigitalLibraryDbContext`,
one `IEntityTypeConfiguration<T>` per entity (20 files; 3 more entities — `WorkAuthor`,
`ShelfItem`, `PriceHistoryEntry` — are configured inline as owned collections of their
parent aggregate), the first migration (`InitialCreate`), a `docker-compose.yml` stack
(`db` + `api`), and a `DevelopmentSeeder` (2 works, 3 editions, 2 library items).
Verified end-to-end against a real Postgres container, not just "it compiles": ran
`docker compose up --build -d`, confirmed the migration applied, the seed landed, and
both health endpoints returned 200.

## Why we built it this way

### EF Core does not support discriminator-based inheritance for owned types
The plan (section 7) specified `ProgressPoint` as "an owned type with a discriminator +
page_value/percent_value/position_ticks nullable columns." This turned out to be
unbuildable: `OwnedNavigationBuilder<TOwner,TDependent>` has no `HasDiscriminator` method
at all (verified directly against the EF Core 10.0.12 XML docs — only plain
`EntityTypeBuilder` and the newer `ComplexPropertyBuilder` have it, and neither fits
cleanly here). Raised this to the project owner with two options (a single JSON column,
or flattening `ProgressPoint` into `ProgressEntry`'s own fields); the owner chose the
flattening approach and specified the exact shape.

**Resulting design:** `ProgressEntry` is a normal entity in its own `reading_progress`
table (not owned by `ReadingSession`), with `Point` computed from four private fields
(`_kind`, `_pageValue`, `_percentValue`, `_positionTicks`) rather than stored directly.
`ProgressEntryConfiguration` maps those fields by name (`b.Property<T>("_fieldName")`) —
EF Core reads/writes private fields via reflection regardless of access modifier, so no
`InternalsVisibleTo` is needed. A table check constraint
(`ck_reading_progress_single_value`) enforces at the database level that exactly the
column matching `kind` is non-null; the domain's own `switch` expressions enforce the
same invariant in memory. This is the only real deviation from `docs/PLAN.md` in this
stage — the plan file on disk was **not** updated to reflect it (checked byte-for-byte
against origin; no such edit exists), so if this history entry and the plan ever
disagree, the plan is stale on this specific point, not this entry.

### Private parameterless constructors for owned-type-typed parameters
Separately from the above: EF Core's constructor binding cannot pass an owned type as a
constructor argument at all ("references to owned types cannot be bound"), even for a
non-polymorphic owned type like `Money`. Every entity/value object whose business
constructor takes an owned-type parameter — `LibraryItem` (`Acquisition`),
`WishlistEntry` (`Money? MaxPrice`), `PriceHistoryEntry` (`Money`), and `Acquisition`
itself (`Money? Price`, nested one level deeper) — got an additional `private`
parameterless constructor used only by EF, which then sets every field/property directly
via reflection. `Loan` needed the same fix for an unrelated reason: a constructor
parameter with a default value (`DateOnly? dueOn = null`) also failed constructor-binding
validation in a way that a parameterless constructor fixed; the exact EF Core mechanism
behind that specific case wasn't fully pinned down (time-boxed once the fix was
confirmed empirically against a real migration), so if another entity's constructor has
optional/default parameters and hits the same "Cannot bind 'x'" error in Stage 3+, this
is the known fix.

**None of this touches the domain's public API or invariants** — the extra constructors
are private, do nothing but exist for reflection, and every business rule still lives in
the original constructors and methods.

### Value object mapping: scalar conversion vs. owned type
Single-field wrapper VOs (`Isbn`, `AudioDuration`, `SeriesPosition`) map via
`HasConversion` as plain scalar columns — no nested table/prefix needed for a single
value. Multi-field VOs (`Money`, `PhysicalLocation`, `Acquisition`) map as `OwnsOne`
owned types, including one level of nesting (`Acquisition.Price` is itself an owned
`Money`, which EF Core does support for non-polymorphic owned types).

### No foreign key constraints across aggregate boundaries
Plan section 3.8: "aggregates reference each other only by Guid, not by navigation
property." Taken literally into the DB schema too — `LibraryItem.EditionId`,
`ReadingSession.LibraryItemId`, `Note.LibraryItemId`, etc. are plain indexed `uuid`
columns with **no** FK constraint to their target table. Intra-aggregate child
collections (`WorkAuthor` under `Work`, `ShelfItem` under `Shelf`, `PriceHistoryEntry`
under `BookstoreListing`, `ProgressEntry` referencing `ReadingSession`) do get real FK
constraints with `Cascade` delete, since those are genuinely owned/dependent rows, not
cross-aggregate references. This is a stricter reading of the plan than "just don't add
navigation properties" — worth revisiting if Stage 3's query layer turns out to need
referential integrity across aggregates for correctness rather than just convention.

### `Author.ExternalIds` needed an explicit `ValueComparer`
`SetExternalId` mutates the backing `Dictionary<string,string>` in place, and EF Core's
change tracker compares converted values by reference unless told otherwise — an
in-place dictionary edit would silently fail to be picked up by `SaveChanges()`.
Caught this from an EF startup warning during the `docker compose up` verification run
(not from reading docs), fixed with an explicit `ValueComparer` doing a structural
`SequenceEqual`/hash/deep-copy.

### Central Package Management (`Directory.Packages.props`)
Not originally planned for this stage, but became necessary: `MyDigitalLibrary.Api` and
its `IntegrationTests` project both pull `Microsoft.EntityFrameworkCore.*` transitively
through project references (`Api` → `Infrastructure`), and NuGet resolved a lower floor
version (10.0.4, from `Npgsql.EntityFrameworkCore.PostgreSQL`'s own dependency range)
than `Infrastructure` itself got (10.0.12, forced up by its direct `Design` package
reference) — a classic diamond-dependency warning (MSB3277). Pinning packages
project-by-project just moved the conflict to a different project each time. Central
Package Management with `CentralPackageTransitivePinningEnabled` fixes the whole
solution's dependency graph in one place instead, which will keep paying off as more
EF-Core-adjacent packages (Testcontainers, InMemory, etc.) get added in later stages.

### Docker Compose port offsets
`svara-bg` (a different project on this machine) already occupies 5432, 5433, 5434,
5435, and 8080 with its own compose stack. `docker-compose.override.yml` publishes this
project's `db`/`api` on 5532/8081 instead of the plan's implied defaults, purely to avoid
colliding with that unrelated, already-running stack — not a general recommendation, just
what was free on this machine at the time.

## Files created

- `src/MyDigitalLibrary.Infrastructure/Persistence/MyDigitalLibraryDbContext.cs`
- `src/MyDigitalLibrary.Infrastructure/Persistence/DesignTimeDbContextFactory.cs`
- `src/MyDigitalLibrary.Infrastructure/Persistence/Configurations/*.cs` (20 files, one per
  non-owned entity)
- `src/MyDigitalLibrary.Infrastructure/Persistence/Seed/DevelopmentSeeder.cs`
- `src/MyDigitalLibrary.Infrastructure/Migrations/20260911114424_InitialCreate.cs` (+
  Designer + snapshot)
- `docker-compose.yml`, `docker-compose.override.yml`, `.env.example`, `.dockerignore`
- `src/MyDigitalLibrary.Api/Dockerfile`
- `Directory.Packages.props`
- Domain: added private EF-only constructors to `LibraryItem`, `WishlistEntry`, `Loan`,
  `PriceHistoryEntry`, `Acquisition`; rewrote `ProgressEntry`'s internals (public API
  unchanged)

## What's still open

- The `PhysicalLocation` "optional dependent using table sharing" EF warning is expected
  and left alone — the domain's own constructor already forbids the ambiguous
  all-null-fields case, so the warning doesn't correspond to a reachable bug. Revisit if
  EF's behavior around it ever changes.
- `libgssapi_krb5.so.2` missing in the `aspnet:10.0` runtime image produces a harmless
  Npgsql startup message (GSSAPI auth probing; we use password auth, not Kerberos). Not
  fixed — would mean adding `krb5-libs` to the Dockerfile for a feature we don't use.
- No Infrastructure-level automated tests yet (Testcontainers-backed, per plan section
  11) — verification for this stage was a real `docker compose up` + manual `psql`/`curl`
  checks. Stage 3/4 should add the actual Testcontainers test project once there's a
  query/API layer worth testing against a real database.
- `docs/PLAN.md` still shows the original (unbuildable) `ProgressPoint` owned-type
  mapping in section 7. Not edited by this session — flagging here so Stage 3+ doesn't
  get surprised by the mismatch.

## Next

Stage 3 — CRUD API: `library-items`, `works`, `editions`, `wishlist` resources,
`ProblemDetails`, pagination, validation, OpenAPI.
