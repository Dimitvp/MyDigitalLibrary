# Audible "finished" library import — format-correcting 20 Goodreads-tagged books

**Date:** 2026-09-17
**Context:** The owner exported their Audible library (`audible_library_finished.csv`,
40 audiobooks, all `Status=Finished`) and asked to add them as owned, "read"
audiobooks. They flagged upfront that many would collide with existing
`library_items` — books originally bulk-imported from Goodreads, which only
ever recorded *read/want-to-read*, never format, so a prior session had
defaulted all of them to `Ebook`. Instruction: replace those specific
duplicates' format with the Audible-confirmed truth; leave every other
`Ebook`-tagged item untouched for the owner to fix by hand.
**Status:** Complete. All 40 rows processed; 246/246 backend tests unaffected
(no code changes this session — pure data operations through the existing API).
**Commits:** history-only (see `git log`).

## Classification (title + format aware, per the rule fixed 2026-09-16)

Matched all 40 CSV titles against current `library_items`/`wishlist_entries`
(exported to scratchpad `.psv`, same word-overlap matcher as the two prior
Libristo rounds, threshold loosened to 0.6 since Audible titles carry heavy
marketing-subtitle noise: "How to Win an Information War: ... : BBC R4 Book
of the Week"). Then **manually cross-checked every automated result** rather
than trusting it outright — the matcher's own single-content-word guard
(added last round to kill false positives) produces false *negatives* for
short titles, and one word repeated within a title (`"...Language..."` twice
in "The Language Instinct: How the Mind Creates Language") inflates overlap
counts and produces false positives. Caught two real misses this way:
"Propaganda" (owned `Ebook`, Edward L. Bernays) === CSV "Propaganda -
Original Edition" — missed because the owned title is a single word; "Nexus"
(owned `Physical`) === CSV's Nexus row — miscategorized but harmless, since
both its possible categories resolve to the same action (create a new
Audiobook item).

- **20 converted `Ebook` → `Audiobook`** (via the `PATCH /library-items/{id}/format`
  endpoint built 2026-09-16, which also clears now-invalid Edition fields —
  ISBN, page count, cover type — automatically): Misbehaving, Until the End
  of Time, Red Notice, The Blind Watchmaker, How to Win an Information War,
  Propaganda, Freezing Order, The Edge of Knowledge, The Myth of Normal, Bad
  Advice, What We Owe the Future, Winter Is Coming, The Emperor of All
  Maladies, The Paradox of Choice, How to Avoid a Climate Disaster, The
  Nurture Assumption, Books Do Furnish a Life, When, The Selfish Gene, The
  Language Instinct. (Four of these — How to Win an Information War, What We
  Owe the Future, How to Avoid a Climate Disaster, and The Edge of Knowledge —
  are exactly the titles flagged in the previous session's ISBN-mismatch scare;
  this Audible data is the real-world confirmation that "Ebook" was wrong and
  "Audiobook" is right, closing that loop.)
- **18 new owned `Audiobook` items** — 12 as a *new Edition on an already-
  existing Work* (owned in Physical, or only wishlisted so far): How Fascism
  Works, Stolen Focus, Lucky Loser, Disinformation, Nexus, Foolproof,
  Political Order and Political Decay, Misbelief, Ghost in the Wires, How
  Minds Change, 21 Lessons for the 21st Century, Noise; plus 6 as genuinely
  new works: Kids Make Me Angry, Erasing History, Nobody's Girl, We the
  People (Lepore's — kept the full CSV title specifically to not collide with
  the *different*, already-wishlisted Fukuyama book of the same short name),
  This Is Not Propaganda, You Are Not So Smart.
- **2 enriched only** (already owned as `Audiobook`, from earlier sessions):
  God Is Not Great, The Origins of Political Order.

Every item got: narrator (from `Narrators`, joined for multi-narrator
recordings), its own Audible cover art (`m.media-amazon.com` — no bot-blocking
issue this time, unlike Libristo), a `PersonalNote` with the ASIN and rating,
`Acquisition{Method: Bought, Source: Audible}`, and a same-day
start+finish `ReadingSession` (no listen-completion date exists in the
export, so today stands in as an explicit placeholder — flagged to the owner
as such, not asserted as a real date). Of the 27 rows carrying `MyRating`
(1–5), all got `PUT /works/{id}/rating` (discovered mid-session — a real,
working endpoint, `score` 1–10 — the CSV's 1–5 doubled to fit).

## The "no existing-Work path" gap in `LibraryItemService.CreateAsync`

`WishlistService.CreateAsync` supports `WorkId` (attach a new Edition to an
*existing* Work) alongside nested `Work` (create one). `LibraryItemService.CreateAsync`
only supports nested `Work` (always creates a new one) or `EditionId` (reuse
an edition wholesale) — there's no "existing Work, new Edition" door, and no
standalone `POST /editions` either; `Edition` rows are only ever created as a
side effect of a Work-level or Wishlist-level composite create. For the 12
"new Audiobook edition on an existing Work" cases, worked around it rather
than adding new API surface mid-import: created a throwaway `WishlistEntry`
against the existing `WorkId` (which *does* support this), captured the
`preferredEditionId`, immediately `DELETE`d the wishlist entry (the `Edition`
row it created is untouched by that — wishlist deletion has no cascade to
editions), then created the real `LibraryItem` via `EditionId`. Left no trace
(`wishlist_entries` has zero rows matching the throwaway marker text,
confirmed after the run). Not proposing this become the permanent way to do
it — flagging the gap in case a future session wants to add the same
`WorkId`-reuse path to `CreateLibraryItemRequest` that `CreateWishlistEntryRequest`
already has.

## Why we built it this way

**Real consumption data outranks a prior guess.** The Goodreads import's
"Ebook" default was always a placeholder — the owner said so explicitly last
session. Audible's own export is ground truth for *these 20 specific
titles*: not a heuristic, an actual purchase-and-listen record. Converting
matched titles and leaving every other `Ebook`-tagged item alone (per the
owner's explicit scope) draws the line exactly where confidence runs out —
resisted the temptation to "fix" every other still-wrong `Ebook` tag
opportunistically, since the owner was explicit that those need eyes-on
correction, not another automated guess.

**Manual review after automated matching, every round.** Three CSV-reconcile
sessions in three days have each surfaced a *different* failure mode in the
same word-overlap matcher (over-eager stopword-free overlap → generic-word
false positives → single-content-word blind spot → repeated-word count
inflation). No single threshold or rule eliminates all of them at once; the
fix each time has been to tighten the algorithm *and* keep manually
spot-checking short/generic titles rather than trusting the script's output
verbatim — this round it caught "Propaganda" before it became a duplicate
Work.

## Files changed

None — this was a data-only session against the existing API (login via the
seed admin, same scratchpad-script pattern as the two prior import rounds).

## What's still open

- The owner still has other `Ebook`-tagged `library_items` they'll correct by
  hand (audio, or read-in-translation — per their own words last session)
  that this Audible export didn't happen to cover.
- `ReadingSession` dates for all 40 are today's date, not the real
  listen-completion date (none was available in the export) — cosmetic only,
  doesn't affect ownership/format correctness, but worth knowing before
  reading any "when did I finish X" reporting built on this data later.
- Noted, not acted on: `LibraryItemService.CreateAsync` lacks the
  `WorkId`-reuse path `WishlistService.CreateAsync` already has (see above).
