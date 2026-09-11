# Stage 6 — Импорт по линк / ISBN

**Date:** 2026-09-11
**Stage:** 6 (import by link/ISBN)
**Status:** Complete
**Commits:** `484db91`, `4a5a5e4`, `564f069`, `baf1a17`, `231e883`, `0a9700b`, `2d0d778`, `b3f6c6b`
(plan-restoration commit `1b09e86` landed first — see its own section below)

## What we built

The plan's Stage 6 scope: `GenericIsbnPageResolver`, Open Library + Google
Books providers, a review/confirm import screen, `ManualFieldOverrides`, and
cover download. Explicitly out of scope and not touched — bookstores, CSV
import, barcode scanner.

- **`ManualFieldOverrides`** (Domain): an immutable value object tracking
  which Work/Edition fields were set by an explicit manual edit.
  `Work`/`Edition.MarkFieldsOverridden(...)` is called from `PUT
  /works/{id}`/`PUT /editions/{id}` only — never from composite creation,
  since a brand-new entity has nothing to protect yet. `Work`/`Edition.Enrich(...)`
  applies importer-sourced values field-by-field, skipping anything already
  overridden. Persisted as a single `jsonb` column via a scalar
  `HasConversion` (same "wraps a single value, not an owned type" pattern
  already used for `Isbn`), not EF's owned-type machinery.
- **Import pipeline** (Application): `BookMetadataCandidate` (the
  provider-DTO-free shape), `IBookLinkResolver` + `CompositeBookLinkResolver`
  (chain of responsibility), `IBookMetadataProvider` +
  `CompositeBookMetadataProvider`, `MetadataMergePolicy` (first
  non-empty-per-field wins, in provider priority order), and
  `ImportLookupService` orchestrating `POST /api/v1/import/lookup`:
  url-or-isbn → ISBN → merged candidate. Entirely read-only — no
  persistence happens here.
- **`GenericIsbnPageResolver`** (Infrastructure): AngleSharp-parsed HTML,
  tried in order — JSON-LD `Book` nodes, `books:isbn`/`itemprop` meta tags,
  `itemprop` microdata, then a regex over visible text — stopping at the
  first checksum-valid ISBN.
- **`OpenLibraryProvider`/`GoogleBooksProvider`** (Infrastructure): map each
  provider's real, hand-verified response shape into `BookMetadataCandidate`.
  A provider failure (including Google's unauthenticated rate limit) is
  treated as "no result," never surfaced as an API error.
- **Cover download** (Infrastructure): a bounded in-process `Channel` +
  `CoverDownloadBackgroundService` — enqueued by `BookCatalogService` right
  after `CreateEditionAsync`, drained off the request path so the book save
  never waits on a cover fetch. Stores the original file only, named by
  content SHA-256, served back under `/covers` via static files.
- **`POST /api/v1/import/lookup`** (Api): the only new endpoint. The
  review/confirm screen does **not** get its own save endpoint — it POSTs
  through the same composite `POST /library-items`/`POST /wishlist`
  endpoints the manual add-book form already uses (plan section 4).
- **`ImportPage`** (Angular): search step (URL or ISBN, auto-detected) →
  editable review step pre-filled from the candidate → confirms through the
  existing `LibraryApiService.create()`.

## Why we built it this way

### Verified against real external services before writing a line of provider code

Per rule 2, both provider integrations were checked against the live
services first: a real `curl` against Open Library's
`/api/books?bibkeys=...&jscmd=data` (confirmed richer and more directly
usable than `/isbn/{isbn}.json`, which just 302-redirects), and Google
Books' own discovery document for the `Volume` schema, since the live
`volumes?q=isbn:...` endpoint was rate-limited (429 `RESOURCE_EXHAUSTED`)
*unauthenticated* at verification time from this network — confirming in
practice the plan's own hedge that "works without a key at low volumes"
isn't fully reliable. Both `ToCandidate` mappers are pure functions,
unit-tested against these verified real shapes without a live network
dependency; a 429/failure is swallowed as "no result," same as a genuine
not-found, so one provider being unavailable never breaks the lookup.

### The typed-HttpClient DI gotcha, caught before it shipped

`AddHttpClient<GenericIsbnPageResolver>()` (and the two providers) makes the
DI container responsible for constructing that class with a correctly
wired `HttpClient`. Registering `IBookLinkResolver`/`IBookMetadataProvider`
the naive way — `AddScoped<IBookLinkResolver, GenericIsbnPageResolver>()` —
would have made the container try to build a *second*,
independently-constructed instance outside the typed-client system, where
`HttpClient` has no plain DI registration — a runtime "unable to resolve
service for type HttpClient" that a passing `dotnet build` would never
catch. Fixed by registering the interfaces as factories that resolve the
already-configured typed-client instance: `sp =>
sp.GetRequiredService<GenericIsbnPageResolver>()`.

### Composite-create's client-facing DTO didn't actually carry `coverUrl`

Manual end-to-end verification (not just `dotnet build`) caught this: the
backend `Catalog.CreateEditionRequest` got a `CoverUrl` parameter, the
domain enqueued correctly when tested directly, but a real `POST
/library-items` with `"coverUrl"` in the nested `edition` object never
triggered a download. The client-facing `NestedEditionInput` DTO (the one
actually bound from the request body) had no `CoverUrl` field at all — the
value was silently dropped on JSON bind, and `LibraryItemService.CreateAsync`
manually re-constructed the internal `CreateEditionRequest` field-by-field,
so nothing forwarded it either. Fixed by adding the field to
`NestedEditionInput` and threading it through the manual reconstruction.
Caught and fixed before commit, not left as a known gap — exactly the kind
of thing that a compiling, plausible-looking implementation can hide until
it's run for real end to end.

### `.gitignore`'s `covers/` was swallowing this stage's entire source folder

Discovered while staging: the Stage 0 scaffold's `covers/` gitignore entry
(intended for the local runtime cover-storage directory) wasn't anchored to
the repo root, so it also matched
`src/MyDigitalLibrary.Infrastructure/Import/Covers/` — this stage's own
source code was silently untracked. `git add` on that path warned rather
than failing quietly, which is how it was caught. Fixed by anchoring the
pattern to `/covers/`.

### Dropped SixLabors.ImageSharp mid-stage — a real, verified blocker, not a guess

Cover resizing (thumb/full, plan 5.6) was implemented against ImageSharp
first. `dotnet build` only warned about a missing Six Labors license, so it
looked shippable — until `dotnet publish -c Release` (the actual Docker
build) turned that warning into a hard error. Traced to the package's own
MSBuild target: `ContinueOnError="$(Configuration.StartsWith('Debug'))"` —
license validation is fatal specifically in Release, never in Debug. The
license text itself (`Six Labors Split License`, fetched and read directly)
confirms this project qualifies for the free tier (non-commercial, under
$1M revenue) — but the *enforcement mechanism* still requires a registered
key from a Six Labors account regardless of which tier applies, which is an
external signup decision, not mine to make. Raised to the user rather than
silently working around it or silently blocking; the user chose to drop
ImageSharp entirely for v1 — store the original cover only (still
size-capped and content-type-whitelisted, no image library needed for
those), revisit with SkiaSharp or Magick.NET later if resizing is ever
actually needed, and verify *their* licensing fresh at that point rather
than assuming.

### `ManualFieldOverrides.Enrich` has no live caller yet — by design, not oversight

The only Stage 6 endpoint is `POST /import/lookup`, which never touches an
existing catalog entity (it's advisory/preview only). The composite-create
path (used by both the manual form and the import-confirm screen) always
creates brand-new Work/Edition rows, so there's nothing yet for
`ManualFieldOverrides` to protect *in a live request path* — it exists as a
fully domain-tested capability (including the plan section 11-required
"manual edit survives repeat enrichment" scenario) ready for whichever
future stage adds a "re-enrich an existing catalog entry" endpoint. Building
that endpoint now would have been scope creep the plan doesn't ask for.

## Restoring `docs/PLAN.md` (commit `1b09e86`, before any Stage 6 code)

Rules 9-10 (section 0) and the full section 7.1 worked example had been
deleted by an earlier commit (`948b10c`) without being superseded — restored
verbatim/faithfully from `da30bf5`. Rule 11 (the mirror case: when shipped
code turns out right and an earlier plan draft was wrong, fix the plan to
describe the code) never made it into any commit at all — it only ever
existed as an in-session instruction, so it's a reconstruction in rules
9-10's own voice, not a byte-exact restore, and is flagged as such in the
commit message. Section 4.1 was deliberately **not** reverted to `da30bf5`'s
version despite being more verbose — that draft describes an API shape
(`authorIds`, nested `edition.id`) superseded by what's actually
implemented and what a later in-session correction confirmed as final
(`authorNames`, top-level `editionId`); restoring it would have
reintroduced a decision the project no longer holds, not just lost prose.

### The `covers` Docker volume was missing until the final verification pass

Plan section 10 lists a `covers` volume mounted into the `api` container
alongside `pgdata`; the initial Stage 6 work only set
`CoverStorageOptions.RootDirectory`'s relative default and never touched
`docker-compose.yml`. Caught before commit by re-reading section 10 while
writing this history entry, then verified for real: added the volume mount
and `Covers__RootDirectory=/app/covers`/`GoogleBooks__ApiKey` env vars,
brought the stack up, created a library item with a cover, confirmed the
file under `/app/covers` inside the container, **restarted the `api`
container**, and confirmed the same file (same SHA-256 name — correctly
deduplicated) was still there and still served at `GET /covers/{file}` —
proving the volume, not just the container's writable layer, is what's
actually holding the data.

## Files created

- `src/MyDigitalLibrary.Domain/Catalog/ManualFieldOverrides.cs`
- `src/MyDigitalLibrary.Infrastructure/Persistence/Conversions/ManualFieldOverridesConverter.cs`
- `src/MyDigitalLibrary.Infrastructure/Migrations/20260911142956_AddManualFieldOverrides.*`
- `src/MyDigitalLibrary.Application/Import/{BookMetadataCandidate,IBookLinkResolver,CompositeBookLinkResolver,IBookMetadataProvider,CompositeBookMetadataProvider,MetadataMergePolicy,ImportLookupService}.cs`
- `src/MyDigitalLibrary.Application/Abstractions/ICoverDownloadQueue.cs`
- `src/MyDigitalLibrary.Infrastructure/Import/{GenericIsbnPageResolver,OpenLibraryProvider,GoogleBooksProvider}.cs`
- `src/MyDigitalLibrary.Infrastructure/Import/Covers/{ICoverStorage,LocalFileCoverStorage,CoverDownloadQueue,CoverDownloadBackgroundService}.cs`
- `src/MyDigitalLibrary.Api/Endpoints/ImportEndpoints.cs`
- `src/web/src/app/features/import/{import-api.service.ts,import.page.ts,import.page.html,import.page.scss}`
- Domain tests: `ManualFieldOverridesTests.cs`, enrichment tests added to `WorkTests.cs`/`EditionTests.cs`
- Application tests: `tests/MyDigitalLibrary.Application.Tests/Import/*` (the project was an empty scaffold before this stage — added NSubstitute)
- Infrastructure tests (fast, no Docker): `tests/MyDigitalLibrary.Api.IntegrationTests/Import/*`

## What's still open

- No cover resizing (thumb/full) — original file only, per the licensing
  decision above. `ICoverStorage` already abstracts storage so this is a
  local change whenever it's revisited.
- No site-specific `IBookLinkResolver` implementations yet — the plan
  explicitly calls these "later," `GenericIsbnPageResolver` is the only
  (and last-resort) link in the chain for v1.
- `GenericIsbnPageResolver`'s "multiple ISBNs found, ask the user" case
  (plan 5.2 step 5) is narrowed to "take the first" — the single-candidate
  `POST /import/lookup` response shape doesn't support a disambiguation
  flow; a real one is future work if it turns out to matter in practice.
- `ManualFieldOverrides.Enrich` has no live caller yet (see above) — fully
  built and tested, waiting on a future "re-enrich an existing entry"
  endpoint that isn't in any stage's scope yet.
- Cover download has no retry/backoff beyond the resilience handler's
  defaults, and no dead-letter/observability beyond a warning log — fine
  for personal-scale volume, would need revisiting for anything busier.

## Next

Stage 7 per the plan — continue only once the user confirms this history
entry is in and pushed.
