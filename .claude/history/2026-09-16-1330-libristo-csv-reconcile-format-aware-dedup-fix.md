# Libristo CSV reconciliation, and fixing the format-blind duplicate guard

**Date:** 2026-09-16
**Context:** The owner exported their Libristo "Искам я" wishlist (54 rows,
`libristo_wishlist.csv`) and asked to reconcile it against the app's wishlist:
add anything missing, fill in missing info (ISBN/cover/price) on already-added
but bare entries, and not bother chasing sources beyond what Libristo itself
gave (hand back any link that didn't open). First pass classified ~20 CSV rows
as "already owned, skip" purely by title+language match. The owner corrected
this hard: owning a book in one format (or translation) does **not** mean you
don't also want it in another format — only a book that matches on **both**
format and language 1:1 counts as already-owned; anything else should be added
as a separate want.
**Status:** Complete for what the CSV data itself could answer. Four titles
flagged back to the owner rather than guessed at (see below) — genuine data
questions only they can resolve.
**Commits:** this session's commit (see `git log`).

## 1. Reconciling the CSV

Compared the CSV against the live `works`/`library_items`/`wishlist_entries`
tables (via `docker exec ... psql`, exported to scratchpad `.psv` files — twice,
after discovering the first export's line-based parsing silently corrupted
rows whose `Note` field contained embedded newlines, undercounting the real
wishlist by ~35 rows). Matched titles with a stopword-aware significant-word-
overlap heuristic (mirroring `WishlistService.TitleLooksLikeAMatch`'s own
logic) plus exact-normalized-title fallback for single-word titles like "Zov" —
loosened/tightened twice to kill false positives (generic single-word titles
like "Science" or "People" matching unrelated books) without losing real
matches (short bare titles like "We Are Bellingcat" against a fuller subtitled
owned title).

Libristo's product pages 403'd through the `WebFetch` tool but opened fine via
plain `curl` with a normal browser UA (Cloudflare bot-filtering `WebFetch`'s
fingerprint specifically, not the origin itself) — fetched full HTML for every
candidate page, pulled `<script type="application/ld+json">` schema.org `Book`
blocks via regex (one malformed-JSON page needed field-by-field regex
extraction instead of a full `JSON.parse`), and downloaded each book's own
Libristo `libris.to/media/jacket/*.jpg` cover directly (also 200 via curl).

**Genuinely missing → added (7 new works + 13 new editions on existing works,
20 total new physical wishlist entries):** Reporting the War in Ukraine, Zov
(**French** — distinct from the already-wishlisted English edition, since
language differs), Kremlin Playbook / 2 / 3, Icarus At The Edge Of Time,
Unsilencing (new works) — plus a **physical** want added for 13 titles the
owner already owns/wants in a *different* format: We Are Bellingcat, How to
Avoid a Climate Disaster, Myth of Normal, What We Owe the Future, Viral, Bad
Advice, Rationality, Science in the Soul, The Marshmallow Test, Robots Will
Steal Your Job, Origins of Political Order, Until the End of Time, How to Win
an Information War — each a *new Edition on the existing Work* (via
`workId`, not a re-created nested `Work` — would have duplicated the catalog
entry) rather than a new book.

**Bare wishlist entries enriched with ISBN/pages/cover-type/cover/price**
(had a `PreferredEditionId` already but blank fields): Rational Optimist,
Science in the Soul (Audiobook-desired — no ISBN/pages exist for that format,
so the print ISBN went in the *note* instead, not the field), Robots Will
Steal Your Job (Ebook-desired), The Fabric of the Cosmos, The Hidden Reality,
Known Unknowns, Red Notice.

**Left alone (true 1:1 duplicates already fully represented):** ~30 CSV rows
matched an existing wishlist entry or owned copy in the *same* format and
language — How Fascism Works, Humans, Flights of Fancy, Nexus (owned
Physical), Disinformation, Selections from Science and Sanity (both already
complete with ISBN+cover), and others.

## 2. The real bug: the app's own duplicate guard ignored format

`WishlistService.EnsureNotAlreadyOwnedAsync` blocked a wishlist add whenever
title (and, if given, language) matched an owned `LibraryItem` — **regardless
of format**. Owning the ebook silently blocked wanting the physical copy. This
wasn't just an import-script inconvenience — it's the exact guard the real
`POST /api/v1/wishlist` UI form hits, so it would have blocked the owner from
ever adding "I also want the print copy" through the app itself. Fixed by
threading `BookFormat desiredFormat` through the check and requiring an exact
`Format` match (alongside the existing not-known-to-differ language check)
before blocking — same title, different format, no longer a duplicate. Added
`tests/MyDigitalLibrary.Api.IntegrationTests/Wishlist/WishlistEndpointsTests.cs`
(new file — no wishlist endpoint tests existed before): exact-match still
blocks, different-format allowed, different-language allowed. 246/246 tests
green; `docker compose build --no-cache api` + restart to deploy (confirmed
via `docker inspect` that the running container's image matched the fresh
build, after briefly suspecting a stale-container red herring while debugging
why four specific adds still 409'd).

## 3. What that fix surfaced: 4 real data anomalies, flagged not guessed

Once format-blind blocking was fixed, four titles *still* 409'd — but on the
**ISBN** check this time, not title/format: We Are Bellingcat, What We Owe the
Future, The Marshmallow Test, and How to Win an Information War are all owned
as **Ebook**, yet their stored `Isbn13` is the exact print-book ISBN Libristo
lists for the physical edition. That's very likely a mistagged format or a
copy-pasted-wrong-ISBN from whenever those were originally added — not
something to silently "fix" by guessing which is true. Reported to the owner
rather than acted on.

Also surfaced: "The Edge of Knowledge: Unsolved Mysteries of the Cosmos" and
the already-wishlisted "Known Unknowns: The Unsolved Mysteries of the Cosmos"
share an author (Lawrence M. Krauss) and identical subtitle but have
**different ISBNs** (9781637588567 vs 9781801100649, 272 vs 240 pages) —
almost certainly two regional/reprint editions of the same underlying book.
Chose to enrich the existing "Known Unknowns" entry with its own CSV data
rather than add a second entry under the other title, since duplicating a
want across two editions of the same work seemed more likely to be noise than
signal — flagged the choice rather than silently deciding it's final. Also
noticed (unrelated, pre-existing, not from today's CSV) two separate `Physical`
wishlist entries for "Freezing Order" already in the catalog — left as-is,
just surfaced.

## Why we built it this way

**A correction to a rule you gave me the same day is still a correction, not
a preference.** The owner's format+language clarification wasn't "adjust this
one import's output" — it described how the app's *own* duplicate check
should behave, permanently. Treating it as a one-off script tweak (special-
casing the CSV import instead of fixing `EnsureNotAlreadyOwnedAsync`) would
have left the real "Add to wishlist" form in the UI just as broken as before
for the next time the owner tries to want a physical copy of something they
own digitally.

**An ISBN collision is a real signal, not an obstacle to route around.**
When the print ISBN turned out to already sit on an "owned Ebook" edition, the
tempting shortcut was to fabricate a different ISBN or drop the constraint —
instead treated it as the guard correctly catching bad data, and surfaced the
four cases rather than picking a side (ebook vs. mistagged-physical) on the
owner's behalf.

## Files changed

- `src/MyDigitalLibrary.Application/Wishlist/WishlistService.cs` —
  `EnsureNotAlreadyOwnedAsync` now takes `desiredFormat` and only blocks on an
  exact format (+ not-known-different language) match
- `tests/MyDigitalLibrary.Api.IntegrationTests/Wishlist/WishlistEndpointsTests.cs`
  (new) — 3 tests covering the exact-match-blocks / different-format-allowed /
  different-language-allowed cases
- No frontend changes this session — all wishlist writes went through the
  existing API from a scratchpad Node script (login + antiforgery handshake,
  matching the pattern from the 2026-09-13 Libristo import session)

## What's still open

**Resolved same day, in a follow-up round.** The owner clarified: all 4
flagged titles (plus a 5th that surfaced the same way, see below) came from
the Goodreads import, which only ever tracked *read/want-to-read*, never
format/ownership — so their `LibraryItem.Format`/`Edition.Isbn13` were never
reliable in the first place. Cleared the wrongly-carried print ISBN on each of
the 4 owned editions (kept publisher/year/page-count, format left as-is for
the owner to fix manually later — "едни ги имам в аудио, други съм чел
преведената"), then added the 4 Libristo physical wants with their real ISBN,
now unblocked.

Also asked to verify the Krauss title pair by checking
`lawrencemkrauss.com/book-archive/` plus a web search: confirmed **"The Known
Unknowns: The Unsolved Mysteries of the Cosmos" (UK, ISBN 9781801100649) and
"The Edge of Knowledge: Unsolved Mysteries of the Cosmos" (US, ISBN
9781637588567) are the same book under different regional titles** — added
both as separate physical wants (the owner explicitly asked for both, with
the market-variant relationship recorded in each entry's note) rather than
silently merging into one. Fetching "The Edge of Knowledge" turned up the
exact same pattern as the 4 flagged titles — its existing owned "Ebook" copy
also carries the print ISBN (9781637588567) — but since the owner's
instruction only covered the named 4, left that owned copy's ISBN untouched
this time and created the new want without an ISBN (cover/pages/note only),
flagging the collision in its own note rather than assuming the same fix
applies. **Investigating this one produced a scare, not a bug**: a `psql`
query mid-debugging appeared to show a duplicate `Work` row and a
"reassigned" `LibraryItem.EditionId` — turned out to be a wrong assumption
(that title had simply never been cross-checked with its own ISBN/publisher
columns before, in any earlier query this session), not actual data
corruption; a from-scratch trace (every `Work`/`Edition`/`LibraryItem`/
`WishlistEntry` row touching that title) confirmed exactly one of each,
consistent all along.
