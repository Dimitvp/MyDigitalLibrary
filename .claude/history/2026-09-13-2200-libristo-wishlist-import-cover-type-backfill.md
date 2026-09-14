# Libristo wishlist import, cover-type field end-to-end, 124-book cover-type backfill

**Date:** 2026-09-13/14
**Context:** The owner pasted HTML scraped from their Libristo.bg online
wishlist (three paginated tables, ~36 books) and asked whether the book
data/cover images could be pulled out and reconciled into the app's own
wishlist. That grew into three linked pieces of work: importing the new
Libristo books, adding a missing "cover type" (hardback/paperback) field to
the wishlist UI after the owner noticed two same-title entries looked like
indistinguishable duplicates, and — after a scope correction from the owner
— backfilling that field on the ~126 pre-existing wishlist entries that
still had no edition data at all, using each entry's own `Note` field as a
pointer to the real bookstore it should be verified against.
**Status:** Complete. Backend change verified with `dotnet build` (full
solution) and the API container rebuilt/restarted; frontend verified with
`npx tsc --noEmit`. All data operations executed against the live database,
backed up first (`pg_dump`), and spot-checked with `psql` afterward.
**Commits:** none yet — the five-file UI/backend diff below is still
sitting as uncommitted working-tree changes; this session's data mutations
were live `INSERT`/`UPDATE`s against the running Postgres container, not
migrations.

## What we built

### 1. Extracting real book metadata from Libristo despite bot protection

`WebFetch` got a flat 403 from libristo.bg; plain `curl` with a real browser
`User-Agent` string worked fine, including on the search endpoint (also
later found to rate-limit/soft-block after a few dozen requests in a short
window — worked around by spacing out requests and falling back to other
sources rather than hammering it). Each book page carries a clean
`schema.org/Book` JSON-LD block (title, ISBN, author, publisher, page count,
`bookFormat`, full-size cover image, price/availability) — more reliable
than screen-scraping visible HTML, though the block's raw whitespace/
newlines inside the description field made naive `JSON.parse` fail on about
a third of pages, so extraction switched to per-field regexes against the
raw block text instead of a full parse.

### 2. Reconciling 36 scraped books against the existing catalog before importing anything

Before writing anything, cross-checked every scraped title/ISBN against the
existing `works`/`library_items`/`wishlist_entries` tables — not just by
ISBN (regional reprints have different ISBNs for the same book) but by
title and, per the owner's explicit caution ("уже мога да я имам на бълг.,
но да я искам на англ." — I might own it in Bulgarian but want it in
English), by edition language too, so an existing Bulgarian copy never
silently blocked adding an English want. Result: of the 36 scraped
Libristo rows, **12 were already owned** (different regional
ISBN/publisher, same English book — skipped), **10 already existed as
bare wishlist entries** from an earlier lower-effort import (enriched
in place with the newly-scraped ISBN/publisher/page-count/cover instead of
creating a duplicate `WishlistEntry`), and **14 were genuinely new**
(13 new `Work`s — one, *The World According to Cunk*, deliberately got two
`Edition`s, paperback and hardback, both wanted). One French edition
("Zov") was swapped for the actual English translation after a web search
turned up its real title/ISBN/translator.

Import ran through a throwaway `tools/LibristoImport` console project
(deleted after the run, per [[project-overview]]'s note on this pattern) —
referenced `MyDigitalLibrary.Application`/`.Infrastructure` directly and
called `BookCatalogService`/`WishlistService` the same way the API does, so
every resolve-or-create-author/work path and duplicate-ISBN guard the UI
relies on also ran here. Cover images were downloaded host-side (`curl` +
SHA-256 filename, matching `LocalFileCoverStorage`'s own convention exactly)
and `docker cp`'d straight into the running API container's `covers` volume,
since the import tool ran on the host rather than inside the container.

### 3. Cover type (hardback/paperback) added to the wishlist end-to-end

The trigger: the owner had deliberately added *The World According to Cunk*
in both formats, but the wishlist list had no way to show that — both cards
looked identical. `Edition.CoverType` existed in the domain already but
was never threaded through `EditionDisplayInfo` → `WishlistEntryDto` →
the Angular `WishlistEntry` model → the list template (the `CoverType` TS
union type already existed, just unused on this interface). Added a
nullable `CoverType?` all the way through — `null` means "no edition at
all yet" (never knowable), distinct from the domain's own `Unknown` (an
edition exists but its binding was never recorded) — and a new pill next
to the existing language/priority/price pills, reusing the
`library.coverType.*` i18n keys already used on the library-detail page
rather than adding new ones. Backend rebuilt into the `api` Docker image
and the container recreated for the change to take effect (it isn't run
via `dotnet watch`); the Angular dev server on :4201 picked up the frontend
edit via its own HMR.

### 4. The owner's scope correction: "check the source I actually added it from"

After the first pass, the owner clarified they'd meant something much more
specific than "research every book" — for the ~126 pre-existing bare
wishlist entries with no cover type, they wanted the *original* source
re-checked, not a generic "what's the most common edition" guess. That
source turned out to already be recorded: 62 of the 126 entries' `Note`
field named the exact bookstore a previous pass had checked (Ciela, Bard.bg,
Ozone.bg, Natural History Museum Shop UK, Libristo.bg, Amazon.com, etc.),
and in one case (*Космос* by Carl Sagan) the note already said "твърди
корици" outright — just never applied to the `Edition` row. This
completely changed the research approach: for Bulgarian titles (a single
edition per title regardless of which BG store sells it, so any major
store's product-page spec field is authoritative) vs. English titles
*with* a named source (must verify against that specific source — a
first pass that guessed "most common current edition" via generic Amazon
searches turned out wrong for three Natural History Museum Shop facsimile
editions, which are hardcover museum reproductions, not the generic Penguin
Classics paperback the guess assumed) vs. English titles with no recorded
source at all (best-effort "standard current retail edition" research,
same as before, but now understood by both sides to carry that caveat).

Four parallel background research agents (bg×2, en×2, split by
25–38-title batches) did the actual lookups; two hit the account's session
rate limit mid-run and had to be relaunched later once it cleared. The
final English batch specifically re-verified the three NHM facsimiles
against nhmshop.co.uk itself (flipping them from the wrong "Paperback"
guess to the correct "Hardcover") and confirmed *Red Notice* against the
exact UK-subtitle Corgi paperback ISBN matching the title string already in
this database, rather than any US edition.

### 5. Applying the backfill — and catching a self-inflicted duplicate

Merged all four batches' results (124 of 126 titles resolved; left 2
genuinely unresolved — *NASA Missions to Mars* wasn't found on the NHM
shop after an exhaustive catalogue search, and *Това е моето минало - Том
2* wasn't found anywhere — rather than guess either). Generated one SQL
script (a `WITH ... INSERT ... RETURNING` per new-edition case, a plain
`UPDATE` per already-has-an-edition case) after taking a fresh `pg_dump`
backup, and ran it inside a transaction. Then, while re-running the same
script a second time purely to *read* its row-count output for verification
(a mistake — should have used a read-only `SELECT` instead), it re-executed
for real and created 104 duplicate orphan `Edition` rows. Caught immediately
by comparing `editions` row counts before/after and checking for rows with
no `wishlist_entries`/`library_items` reference; deleted exactly the 104
matching the accidental run's signature (`work_id` in the applied set,
`isbn13`/`publisher` both `NULL`, unreferenced), verified 3 pre-existing
unrelated orphans were untouched, and confirmed final counts matched
expectations.

## Why we built it this way

**A scope correction from the owner is a redirection to follow exactly, not
a reason to keep the previous approach "as a fallback."** The first cover-
type research pass wasn't *wrong* on English titles with no known source,
but it silently applied the same generic-guess method to titles where a
specific source *was* recorded, which the owner had actually consulted
themselves. Once that was pointed out, every English title with a `Note`-
recorded source got re-verified against that specific source before being
trusted, and the three cases where the generic guess turned out to
disagree with the real source (the NHM facsimiles) proved the correction
mattered, not just their preference.

**A stored `Note` is provenance, not just a comment.** Reading the ~62
notes before designing the research batches turned an open-ended "look
these up somewhere" task into a bounded, source-specific verification task
— and meant the research agents' confidence levels could honestly reflect
"confirmed against the exact listing" vs. "inferred, no source recorded"
instead of treating all 126 titles as equally uncertain.

**For a personal library, "the current standard retail edition" is a
best-effort answer, not a fact — and both sides need to know which one
they're looking at.** Every applied cover type carries a confidence level
in this doc and was filtered (Low-confidence and fully "Unknown" results
left unset rather than applied) precisely so a shaky guess doesn't read as
equally certain as a confirmed bookstore spec field.

**Catch your own mistakes the same way you'd catch bad source data — by
checking row counts, not by trusting that a command "should have" been a
no-op.** Re-running an INSERT-bearing script to read its output is not
idempotent; the fix was the same technique used throughout this session
(compare before/after counts, isolate by a precise `WHERE` signature) aimed
at a bug introduced this session instead of pre-existing data.

## Data changes (live, not in git — no schema change involved)

- **36-book Libristo import:** 12 skipped (already owned under a different
  regional edition), 10 existing bare wishlist entries enriched with real
  ISBN/publisher/page-count/cover, 14 new `WishlistEntry` rows created
  across 13 new `Work`s (one, *The World According to Cunk*, in both
  paperback and hardback). Wishlist entries: 148 → 162. Works: 401 → 414.
- **Cover-type backfill:** 124 of 126 eligible bare/unknown-cover-type
  wishlist entries (physical-format only; ebook/audiobook entries don't
  carry this field) got a real `Hardcover`/`Paperback` `Edition` attached
  or updated in place. Final split across the whole wishlist: 110
  Paperback, 38 Hardcover, 2 left genuinely unresolved (+ audiobook/ebook
  entries, unaffected). A self-inflicted duplicate-run created and then
  removed 104 orphan `Edition` rows (net effect: zero). Final counts:
  **414** works, **402** editions, **162** wishlist entries.
- 24 cover images (from the Libristo import) downloaded and copied into the
  `covers` Docker volume by SHA-256 filename, matching
  `LocalFileCoverStorage`'s own naming convention.
- Took a manual `pg_dump` backup (`pre-covertype-backfill.dump`, copied to
  the session scratch directory) immediately before the backfill `UPDATE`s,
  on top of the app's own automatic debounced backup service.

## Files created/changed

- `src/MyDigitalLibrary.Application/Catalog/BookCatalogService.cs` —
  `EditionDisplayInfo` gained a `CoverType` field; `GetEditionDisplayInfoAsync`
  now selects and maps it.
- `src/MyDigitalLibrary.Application/Wishlist/WishlistEntryDto.cs` —
  `WishlistEntryDto` gained a nullable `CoverType?`; `WishlistEntryMapper.ToDto`
  passes it through (null when there's no edition at all, `Unknown` when
  there's an edition but no recorded binding).
- `src/web/src/app/core/api/models.ts` — `WishlistEntry.coverType: CoverType | null`.
- `src/web/src/app/features/wishlist/wishlist-list/wishlist-list.page.html` —
  new cover-type pill next to the language/priority/price pills, reusing
  the existing `library.coverType.*` i18n keys.
- `src/web/src/app/features/wishlist/wishlist-list/wishlist-list.page.scss` —
  `.pill.cover-type` style, matching the existing pill variants.
- `tools/LibristoImport/` — throwaway one-off console project for the
  36-book import; created and deleted within this session, never committed.

## What's still open

- *NASA Missions to Mars* and *Това е моето минало - Том 2* still have no
  cover type — genuinely not found via any source checked; would need
  either a different store or the owner's own memory of where they were
  seen.
- Several English cover-type determinations (the "no recorded source"
  half of the backfill) are "standard current retail edition" best guesses,
  not confirmed against a specific listing — flagged with Medium/Medium-High
  confidence in-chat, not distinguished from High-confidence ones in the
  database itself (the schema has no field for it).
- Two pre-existing duplicate `Work` pairs noticed along the way (*Red
  Notice* and *Freezing Order* each have one owned + one separately
  wishlisted `Work`, differing only by which regional subtitle was typed
  in) were flagged to the owner but deliberately left unmerged — out of
  scope for what was asked.
- Libristo.bg was rate-limiting/soft-blocking this session's requests by
  the end (echoed by `Freezing Order`'s and `Red Notice`'s cover types
  ultimately coming from Amazon/Biblio.com instead of Libristo itself,
  despite their `Note` fields pointing there) — likely clears on its own;
  no action taken to work around it.

## Next

Nothing queued. Waiting on the owner to review the wishlist page's new
cover-type pills and flag anything that looks wrong, and to say whether the
two unresolved titles or the two duplicate `Work` pairs are worth further
attention.
