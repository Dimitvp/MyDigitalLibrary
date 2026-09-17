# Audible "unread" library import — 11 owned, not-yet-finished audiobooks

**Date:** 2026-09-17
**Context:** Follow-up to the same-day "finished" Audible import. This second
export (`audible_library_unread.csv`, 11 rows) is books the owner bought but
hasn't finished — mostly `Not started`, three `In progress`. The owner's
message itself contained a copy-paste error: "The Stasi" was named twice with
contradictory progress info (95% "не знам защо не 100%" vs "пише че е на 9 но
не съм я чел"). The second mention's numbers (9%, "not started") matched
`Merlin's Tour of the Universe...` in the CSV, not The Stasi — asked rather
than guessed; owner confirmed: **The Stasi is finished** (despite Audible's
95%), **Merlin's Tour is not started** (despite Audible's 9%, apparently a
stray tap).
**Status:** Complete. All 11 rows imported; no code changes (pure data
session against the existing API, same pattern as the two prior imports).
**Commits:** history-only (see `git log`).

## What got created

Checked all 11 titles against `library_items`/`wishlist_entries` first (same
title+format+language discipline as the last two sessions) — two already had
a Work in the catalog under a *different* format, so got a new Audiobook
edition attached to the *existing* Work rather than a duplicate:
- **Homo Deus: A Brief History of Tomorrow** — already owned as `Physical`.
- **The God Delusion** — already wishlisted as `Physical` (as "The God
  Delusion: A Study of Religious Belief and Skepticism").

(Also noticed, not touched: a *third* Homo Deus variant already sits in the
wishlist as `Ebook`-desired under yet another subtitle, "A History of
Tomorrow" — same pre-existing multi-subtitle-duplicate pattern flagged in
earlier sessions, left alone.)

The other 9 were genuinely new works: 22 Cells in Nuremberg, This Is the
Plan, Outgrowing God, Righting Wrongs, Death of a Dissident, The Stasi,
Finding My Way, Merlin's Tour of the Universe, The Curse of God.

Every item: owned (`Bought`/`Audible`), narrator, Audible cover art, and a
`PersonalNote` with ASIN + Audible's own runtime string (no rating this round
— an unfinished book has nothing to rate). Reading status handled per-book
rather than uniformly, since this export (unlike the "finished" one) actually
carries meaningful in-progress state:
- **This Is the Plan** — a `ReadingSession` started today, left open
  (`ReadingStatus.Reading`) — the owner is actually mid-listen.
- **The Stasi** — started *and* finished today (owner-confirmed complete,
  Audible's 95% notwithstanding).
- **Everything else, including Merlin's Tour** — no `ReadingSession` at all,
  so it reads as not-started — correct for genuinely unstarted books, and
  specifically correct for Merlin's Tour where the owner said the 9% Audible
  shows isn't real progress.

## Why we built it this way

**A contradiction in the ask is a stop sign, not a coin flip.** The owner's
message named "The Stasi" twice with two different, mutually exclusive
progress claims. The second occurrence's actual numbers lined up exactly with
a different book already sitting in the same CSV — high-confidence, but still
a guess about which of two real records to write into a permanent library.
Asked in one short, concrete question (showing the numbers, not just "which
did you mean") rather than silently picking the statistically likely reading;
a wrong guess here writes a false "finished" or "unstarted" fact that nothing
downstream would ever flag as wrong on its own.

**Reading status has three states here, not two.** Both prior Libristo/
Audible rounds only ever had "own it or don't" and "ISBN differs" style
decisions. This one needed the app's actual `Reading`/`Finished`/not-started
distinction used correctly per book, instead of the "same-day start+finish"
shortcut that worked for the "finished" export — collapsing "This Is the
Plan" into an instant finish would have manufactured a false completion
alongside the in-progress reality the owner described.

## Files changed

None — data-only session, same scratchpad-script pattern (login + antiforgery
handshake, throwaway-wishlist-entry trick for attaching a new Edition to an
existing Work — see 2026-09-17's earlier history entry for why that
workaround exists) as the last two import rounds.

## What's still open

Nothing queued from this round specifically. The third Homo Deus subtitle
variant (Ebook-desired wishlist entry, distinct from both the now-Physical-
and-Audiobook-owned copy and this session's new Audiobook edition) remains
unreconciled, noted but not acted on, consistent with not touching
pre-existing multi-subtitle duplicates without being asked.
