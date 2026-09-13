# Followed book sources, wishlist out-of-stock flag, and the port-4200 collision

**Date:** 2026-09-13
**Context:** The owner had a personal `Книги.docx` on the desktop — 100+ links
collected over time from Ozone, Ciela, Bard, Helikon, Iztok-Zapad, Millenium,
Natural History Museum Shop, and a dozen other bookstores/publishers/archives,
mixed with a handful of links that weren't books at all (LibGen, a
Sapolsky reading list, other stores' own wishlist pages). The ask, in stages:
open every link, add what's clearly a specific book, list what isn't for
manual review, and — separately — build a proper place in the app to track
the non-book links (stores/libraries followed, not owned/wanted books). Once
the review list came back, the owner asked to actually add everything
addable, flag the ones found out-of-stock (bright red, both in the list and
inside the entry) rather than skip them, and trim the review artifact down to
only the genuine unresolved problems. Separately, mid-session, the owner
reported "a problem with Docker" while using the app in the browser.
**Status:** Complete. All three pieces shipped and verified against the live
`docker compose` stack; 233/233 backend tests green throughout.
**Commits:** this session's commit (see `git log`).

## 1. New feature: followed book sources ("Книжарници")

A `WishlistEntry` needs a `Work` — it's a specific book. Roughly a quarter of
the docx's links weren't books at all: LibGen, Libristo's own wishlist page,
Robert Sapolsky's course reading list, and ~20 bookstore/publisher/archive
homepages the owner just wants a standing bookmark list for ("за да мога от
там да ги следя"). Rather than stuff those into a note field somewhere, built
a small parallel feature, deliberately kept as free-text as `BookstoreListing`
(Stage 8's per-edition availability tracker) is structured — no adapter, no
scraping, just "remember to check this site again":

- **Domain:** `FollowedBookSource` (`Domain/Library/FollowedBookSource.cs`) —
  `UserId`, `Name`, `Url`, nullable `Category`, nullable `Notes`, `AddedOn`.
  `Update()` mirrors `Note`'s shape (validate non-blank name/url, replace the
  rest). No adapter key, no availability enum — this is a bookmark, not a
  listing.
- **Persistence:** `FollowedBookSourceConfiguration`, `DbSet` wired into both
  `IApplicationDbContext` and `MyDigitalLibraryDbContext`, unique index on
  `(UserId, Url)` so the same link can't be followed twice. Migration
  `20260913113315_AddFollowedBookSources`.
- **Application/API:** `FollowedBookSourceService` (list/create/update/delete,
  same `NotFoundException`/`AppValidationException` conventions as every
  other service), `FollowedBookSourceEndpoints` at `/api/v1/book-sources`,
  registered in `Program.cs` alongside the other scoped services and endpoint
  maps.
- **Angular:** new `features/book-sources` module — API service, routes,
  and a single list page (`book-sources-list.page.*`) combining the list with
  an inline add form (no separate `/add` route, unlike wishlist — the form
  here is 4 short fields, didn't earn a page of its own) grouped by
  `Category`. Nav link and `/book-sources` route added; `nav.bookSources` /
  `bookSources.*` keys added to both `bg.json` and `en.json`.
- **Tests:** `FollowedBookSourceTests` (Domain) and
  `FollowedBookSourceEndpointsTests` (API integration — add/dup-reject/
  update/delete), same shape as the existing `Note`/`Shelf` test pairs.

**Populated live, not left empty.** Logged into the running dev API as the
seed admin (cookie + antiforgery handshake via `curl`) and added the 23
sources the docx review actually confirmed as stores/libraries rather than
books — Ozone, Ciela, Bard, Helikon, Milenium, Knigomania, Iztok-Zapad, NHM
Shop, COMDOS (State Security archive), LibGen, and others — each tagged
`книжарница` / `издателство` / `библиотека/архив`. Left out `zamzar.com` (a
file converter, not a book source at all) and the confirmed-dead
`bookdepository.com` (redirects to a generic Amazon page since the 2023
shutdown).

## 2. Wishlist: `IsOutOfStock`, and 68 books added

Research (four parallel agents, one per ~25 links, each opening every URL and
cross-checking blocked ones via search) turned the docx into three buckets:
confidently-identified books, ambiguous links needing a human pick (author
pages, bundles, two listings for what looks like the same book under
different titles), and dead ends. The owner's follow-up: add what's addable,
including the ones confirmed out of stock — just mark them, don't skip them.

That needed a real field, not a string convention in `Note` — `Note` is the
owner's own free text, and sniffing it for a magic substring would've been
fragile the moment they wrote their own note containing that word. Added
`IsOutOfStock` (`bool`, defaults `false`) to `WishlistEntry`, threaded through
the same layers as the followed-sources field: constructor + `Update()` in
the domain entity (`Update()`'s signature grew a parameter — checked first
that nothing currently calls it outside this change, since Stage 9 never
shipped a wishlist-edit endpoint), `WishlistEntryConfiguration`,
`WishlistEntryDto` / `CreateWishlistEntryRequest`, `WishlistService.CreateAsync`,
`WishlistEntry` TS model, `CreateWishlistEntryRequest` TS model (existing
`wishlist-form.page.ts` updated to send `isOutOfStock: false` explicitly).
Migration `20260913121340_AddWishlistEntryIsOutOfStock`.

**UI:** `wishlist-list.page.html`/`.scss` — a card with `entry.isOutOfStock`
gets a full-width bright-red banner reading "Изчерпана" across its top edge
(`--out-of-stock, #d6392b`, a hardcoded strong red rather than a theme token,
since the owner asked for it to read as an unmissable warning regardless of
light/dark theme) plus a matching red card border/glow. There's no separate
wishlist detail page (only list + add exist, unlike library items) — the
banner sits directly on the list card, which is the only "view" a wishlist
entry has.

**Populated live:** logged in fresh (the API container had been rebuilt for
the migration, so a new antiforgery/session handshake was needed — reused
the same curl pattern as the followed-sources seeding). Added 68 entries in
batches of ~15–20 POSTs each:

- **49 available** (or availability unconfirmed because the store's own
  anti-bot page blocked automated checking — treated as available, not
  flagged, since "couldn't verify" isn't the same claim as "confirmed
  unavailable") — spanning Ozone, Bulgarianhistory.shop, Iztok-Zapad, Ciela,
  Communitas, Bard, Millenium's "История на войните" series, Vakon, the
  Natural History Museum Shop's full Darwin/Wallace/dinosaur line, and a
  handful of individually-titled Amazon/Libristo finds (Bill Browder's *Red
  Notice* and *Freezing Order*, Johann Hari's *Stolen Focus*, Chris Kubecka's
  *The Drone Wars*).
- **19 flagged `isOutOfStock: true`** — mostly Ciela's "Минало несвършено"
  history series (all four *Това е моето минало* volumes, *Памет и
  справедливост*, *Семиотика и критика на културата*, etc.), plus Sagan's
  *Комета*, Fukuyama's *Произход на политическия ред*, the *Factfulness*
  authors' book, and Sapolsky's *A Primate's Memoir* — each `Note` records
  which store and that it was checked as unavailable, not just the flag.
- Two books that came back from a mismatched link (Bard.bg IDs the docx's
  handwritten note expected to be Carl Sagan turned out to be Politkovskaya's
  *Russian Diary* and Mitnick's *The Art of Invisibility*) were added anyway,
  with a note explaining the mismatch — they're real, in-stock, confirmed
  books; the label in the original doc was just wrong.
- Discovered mid-verification: the wishlist already held **80 pre-existing
  entries** unrelated to this docx (e.g. *Умни пари* — a personal-finance
  book), all dated today — almost certainly from the owner's own Goodreads
  import work earlier this week. Confirmed via a duplicate-title check (zero
  overlap) that nothing from this session's 68 collided with or overwrote
  that data; final total is 148, all additive.

Docker image rebuilt (`docker compose up -d --build api`) once for each
migration so the running dev stack picked up the schema change before the
API calls landed against it.

**Review artifact trimmed to just the leftovers.** The 68-added and
now-resolved items were removed from the published review page; what's left
is the 14 genuine open questions — author/collection pages needing a specific
pick (Pinker × 2 stores, Brian Greene, the Hawking and "Красив ум" bundles,
the Milenium publisher catalog), two duplicate-edition calls (two different
*Архипелаг Гулаг* listings; a title mismatch between Ciela and Minaloto.bg
for what's probably the same Arendt/Voegelin/Aron book), and five dead/
blocked links (Knigomania's whole catalog returns 403 to automation,
SoftPress's search page, a redirect-looping Helikon link, a vanished
Knizhen-pazar listing, two NatGeo Bulgaria articles with no confirmed direct
product page).

## 3. Dev server port collision (port 4200 → 4201, pinned)

Reported mid-session as "a problem with Docker," seen in the browser while
using the app. Traced it: an unrelated local project's `svara-frontend`
Docker container publishes host port **4200** — the exact port Angular CLI
defaults `ng serve` to, and the one this project's own `README.md` and
`.vscode/launch.json` pointed at. Opening `localhost:4200` was quietly
showing Свара.bg (the card game) instead of the library app; nothing in this
repo had ever pinned a port, so whoever last worked around the 4200 clash did
it by typing `--port 4300` (or `4301`) directly into a terminal, and that
process just... never got closed. `netstat`/`Get-CimInstance Win32_Process`
turned up **two** such zombie `ng serve` processes still listening, from
different past sessions, both fully functional and both proxying to the API
correctly — which is exactly why the owner's memory of "I set it to 4201, how
did it become 4301, does it auto-change?" didn't line up with anything in the
repo: nothing auto-changes it, ad hoc `--port` flags typed in forgotten
terminals do.

**Fix, made durable rather than re-explained:**
- `angular.json` → `architect.serve.options.port: 4201`, so `npm start` /
  `ng serve` always binds there with zero flags needed, ever.
- `README.md`, `src/web/README.md`, `.vscode/launch.json` updated from 4200
  to 4201, each with a one-line note about the Docker collision so the next
  person (or session) hitting this doesn't have to re-diagnose it.
- Killed both zombie processes (PIDs on 4300/4301), started exactly one
  clean `npm start`, confirmed it bound to 4201 and proxies `/health/ready`
  through to the API with a 200.

## Why we built it this way

**A bookmark isn't a listing.** `FollowedBookSource` was kept deliberately
free-text and adapter-less even though `BookstoreListing` (the existing
per-edition availability tracker) was sitting right there as a tempting
thing to extend — conflating "a store I want to remember to browse" with "a
specific edition's price/availability at a specific store" would have forced
every followed source to pretend it's about one book, which it isn't.

**A status needs a field, not a string convention.** `IsOutOfStock` went into
the domain model rather than a marker inside `Note` specifically because
`Note` is the one place the owner's own words live — a substring check would
break the day they wrote a note that happened to contain "изчерпана" for an
unrelated reason, or silently stop matching if they ever edited the note.

**Don't guess data into someone's permanent library.** Where a store blocked
automated verification outright (Knigomania) or a page showed a bundle/author
instead of one book, those stayed on the "needs your pick" list rather than
getting a best-guess title invented and added — wrong metadata in a personal
catalog is worse than an extra manual step.

**Fix the setting, not just the moment.** The port collision would have
recurred indefinitely if the fix had just been "here, open 4300 instead" —
pinning it in `angular.json` (version-controlled, applies with zero flags)
plus killing the actual zombie processes addresses both the immediate
symptom and the reason it kept resurfacing across sessions.

## Files changed

- `src/MyDigitalLibrary.Domain/Library/FollowedBookSource.cs` (new)
- `src/MyDigitalLibrary.Domain/Library/WishlistEntry.cs` — `IsOutOfStock`
- `src/MyDigitalLibrary.Application/Bookstores/FollowedBookSourceDto.cs`,
  `FollowedBookSourceService.cs` (new)
- `src/MyDigitalLibrary.Application/Wishlist/WishlistEntryDto.cs`,
  `WishlistService.cs` — `IsOutOfStock` plumbing
- `src/MyDigitalLibrary.Application/Abstractions/IApplicationDbContext.cs` —
  `FollowedBookSources` DbSet
- `src/MyDigitalLibrary.Infrastructure/Persistence/Configurations/FollowedBookSourceConfiguration.cs` (new),
  `WishlistEntryConfiguration.cs` — `IsOutOfStock` column
- `src/MyDigitalLibrary.Infrastructure/Persistence/MyDigitalLibraryDbContext.cs` —
  `FollowedBookSources` DbSet
- `src/MyDigitalLibrary.Infrastructure/Migrations/20260913113315_AddFollowedBookSources.*`,
  `20260913121340_AddWishlistEntryIsOutOfStock.*` (new), snapshot updated
- `src/MyDigitalLibrary.Api/Endpoints/FollowedBookSourceEndpoints.cs` (new)
- `src/MyDigitalLibrary.Api/Program.cs` — service + endpoint registration
- `tests/MyDigitalLibrary.Domain.Tests/Library/FollowedBookSourceTests.cs` (new)
- `tests/MyDigitalLibrary.Api.IntegrationTests/BookSources/FollowedBookSourceEndpointsTests.cs` (new)
- `src/web/src/app/features/book-sources/**` (new — API service, routes, list+form page)
- `src/web/src/app/features/wishlist/wishlist-list/wishlist-list.page.html`,
  `.scss` — out-of-stock banner/styling
- `src/web/src/app/features/wishlist/wishlist-form/wishlist-form.page.ts` —
  sends `isOutOfStock: false`
- `src/web/src/app/core/api/models.ts` — `FollowedBookSource`,
  `CreateFollowedBookSourceRequest`, `UpdateFollowedBookSourceRequest`,
  `WishlistEntry.isOutOfStock`, `CreateWishlistEntryRequest.isOutOfStock`
- `src/web/src/app/app.html`, `app.routes.ts` — nav link + `/book-sources` route
- `src/web/public/assets/i18n/bg.json`, `en.json` — `nav.bookSources`,
  `bookSources.*`, `wishlist.list.outOfStock`
- `src/web/angular.json` — pinned dev server `port: 4201`
- `src/web/.vscode/launch.json`, `README.md`, `src/web/README.md` — port
  4200 → 4201 references, with the collision noted

## Verified

- `dotnet build` clean across the solution after every schema change.
- Full backend suite green throughout: 86 Domain + 18 Application + 129 API
  integration = **233/233**, including the 4 new followed-source tests.
- `ng build --configuration development` compiled clean after each Angular
  change (templates included, not just `tsc --noEmit`).
- Live `docker compose` stack: API container rebuilt twice (once per
  migration), `/health/ready` → `Healthy` each time, new columns confirmed
  present via `psql \d`.
- 23 followed sources and 148 total wishlist entries (68 added this session
  + 80 pre-existing, zero title overlap) confirmed via authenticated `GET`
  against the live API.
- Port fix: confirmed `localhost:4201` serves "My Digital Library" (not
  Свара.bg) and proxies `/health/ready` with 200, after killing the two
  stray processes and starting exactly one clean instance.

## What's still open

The 14-item "needs your pick" list published as the review artifact — author/
collection pages, two duplicate-edition calls, and a handful of dead/blocked
links. Nothing else queued.

## Next

Whatever the owner picks from the 14 remaining items; otherwise nothing
queued.
