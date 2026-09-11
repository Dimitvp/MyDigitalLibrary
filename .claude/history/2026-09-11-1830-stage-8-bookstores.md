# Stage 8 — Книжарници и наличност

**Date:** 2026-09-11
**Stage:** 8 (bookstore availability tracking)
**Status:** Complete
**Commits:** `97d68bb`, `c910d43`, `e3677f4`

## What we built

Plan section 12's Stage 8 scope: `Bookstore`, `BookstoreListing`, adapters
with configurable selectors, `AvailabilityRefreshService`, price history.
Explicitly out of scope and not touched — email notifications, cross-store
price comparison. Domain (`Bookstore`, `BookstoreListing`,
`PriceHistoryEntry`, `MarketAvailability`) was already complete since
Stage 1; this stage was Application + Infrastructure + Api + tests, plus one
real domain bug found and fixed along the way.

- **`IBookstoreAdapter`** (Application port) + **`SchemaOrgBookstoreAdapter`**
  (Infrastructure): one generic adapter class, not one per bookstore,
  matching the plan's own "selectors are config, not code." Tries a
  schema.org `Book`/`Offer` JSON-LD block first (far more robust than CSS —
  survives a redesign that doesn't touch structured data), falls back to
  the plan's originally-sketched CSS-selector config only if JSON-LD is
  absent.
- **`AvailabilityRefreshService`**: a daily `PeriodicTimer` background
  service, one fresh DI scope per *listing* (not per cycle), per-host
  politeness delay, exponential-backoff-via-resilience-handler, N
  consecutive failures → `Availability = Unknown`, a global
  `Bookstores:Enabled` kill switch.
- **`GET/POST /api/v1/editions/{id}/listings`**, **`PATCH
  .../listings/{listingId}/discontinued`** (plan 6.3's manual override —
  not in section 4's list, added and documented back into the plan like
  Stage 7's shelves/notes/quotes).

## Why we built it this way

### robots.txt checked for real, before any adapter code — and it changed the plan

Plan section 6 opens with an explicit warning to check `robots.txt`/ToS for
all four named bookstores (Ozone.bg, Helikon.bg, Ciela.com, Orange Center)
before writing an adapter, and to skip a site if in doubt. Did exactly
that, for real, before writing `IBookstoreAdapter`'s first implementation:

- **ozone.bg**: `robots.txt` itself returns `403` — "Your network is
  blocked because of heavy bot traffic." The site is actively blocking
  automated access at the network/WAF level. No adapter was written for
  it — building one would mean working around an active bot block
  (fingerprint spoofing, evasion), which is a different, unacceptable kind
  of thing from polite, `robots.txt`-respecting scraping.
- **orangecenter.bg**: `robots.txt` is otherwise permissive but explicitly
  disallows exactly the path pattern a book listing page needs:
  `Disallow: /catalog/product/view/id/*`. No adapter was written for it —
  the site is explicitly asking crawlers to stay off product pages.
- **helikon.bg** and **ciela.com**: both `robots.txt` files are permissive
  (`Allow: /`, only the usual admin/cart/checkout paths excluded), and
  fetching a real product page from each (`helikon.bg/243437-...html`,
  `ciela.com/nakazatelen-kodeks.html`) turned up something better than the
  plan's own sketch: both publish a full schema.org `Book`/`Offer` JSON-LD
  block with `price`, `priceCurrency`, and `availability` as a
  `https://schema.org/InStock`-style URL — exactly the structured,
  redesign-resistant signal `GenericIsbnPageResolver` already knew how to
  parse from Stage 6. `SchemaOrgBookstoreAdapter` was built to try this
  first, with the plan's CSS-selector design kept only as a fallback —
  neither of the two real, shipped adapter configs (`helikon`, `ciela`)
  needs a single configured selector.

This changes what "Etap 8 book availability" actually covers in this
project: 2 of the 4 named bookstores, by design, not oversight — the other
two are exactly the case plan section 0 rule 8 and section 6's own warning
describe ("ако имаш съмнения, недей"). Documented in `docs/PLAN.md`
section 6.1 so a future session doesn't have to redo this research or
wonder why only two adapters exist.

### A real EF Core warning, found by actually running the refresh cycle

`BookstoreListing.RecordSuccessfulCheck` passed the *same* `Money`
instance to both its own `Price` property and the new `PriceHistoryEntry`
it appends — harmless from a pure-domain, value-equality point of view
(`Money` is an immutable record), but EF Core's owned-type tracking model
expects each owned instance to belong to exactly one owner slot. Verified
against a real deployment (added a listing pointing at the real
`helikon.bg` page, restarted the container to trigger the background
service's on-start refresh) and the logs showed it clearly: "The same
entity is being tracked as different entity types
'PriceHistoryEntry.Price#Money' and 'BookstoreListing.Price#Money' with
defining navigations." Fixed by giving the history entry its own `Money`
copy. `BookstoreListing` had zero domain tests since Stage 1 despite real
invariants (never record `OutOfStock` on a failed scrape, consecutive-
failure tracking, the manual `Discontinued` override) — added full
coverage, including a test that pins this exact fix by asserting the two
`Money` values are equal but not the same reference.

### Verified against the real, live sites before committing

Beyond the two adapter-parsing unit tests (against real, saved HTML from
both sites), the whole pipeline was run for real: created a library item,
added a manual listing pointing at the actual `helikon.bg` product page
(in stock, €12.99) and the actual `ciela.com` product page (out of stock,
€1.28), restarted the `api` container to trigger `AvailabilityRefreshService`'s
immediate on-start refresh (the `do`/`while (await timer.WaitForNextTickAsync(...))`
loop runs once immediately on startup, then waits — deliberately, so this
didn't require waiting a real day to verify), and confirmed both listings
came back from the live sites with exactly the real price/currency/
availability shown on the pages, no fabricated data. Also verified the
manual `Discontinued` override and that a listing whose `Bookstore`
`AdapterKey` matches no registered adapter is simply left alone (plan
6.3 — always usable without an adapter, without internet).

## Files created

- `src/MyDigitalLibrary.Application/Bookstores/{IBookstoreAdapter,BookstoreListingDto,BookstoreListingService}.cs`
- `src/MyDigitalLibrary.Infrastructure/Bookstores/{SchemaOrgBookstoreAdapter,AvailabilityRefreshService,BookstoresOptions}.cs`
- `src/MyDigitalLibrary.Api/Endpoints/BookstoreListingEndpoints.cs`
- `tests/MyDigitalLibrary.Domain.Tests/Catalog/BookstoreListingTests.cs` (10 tests — the entity's first ever domain tests)
- `tests/MyDigitalLibrary.Api.IntegrationTests/Bookstores/{SchemaOrgBookstoreAdapterTests,BookstoreListingEndpointsTests}.cs` (11 tests, all passing — 7 network-free adapter-parsing tests against real captured markup, 4 real-Postgres endpoint tests)

## What's still open

- Only 2 of the 4 bookstores the plan names have adapters (helikon, ciela)
  — ozone.bg and orangecenter.bg are deliberately excluded; see above.
  Revisit only if either site's posture changes.
- No Angular UI for bookstore listings — consistent with Stage 7, backend-
  only this session; no stage has explicitly asked for a frontend catch-up
  since Stage 5.
- The CSS-selector fallback path in `SchemaOrgBookstoreAdapter` is
  implemented and unit-tested against synthetic markup, but has no real,
  shipped configuration — neither verified adapter needed it. A future
  bookstore without JSON-LD would be the first real exercise of that path.

## Next

Stage 9 per the plan (the remaining odds and ends — CSV import, export,
full-text search, statistics, reading goals, loans, duplicate detection) —
continue only once the user confirms this history entry is in and pushed.
