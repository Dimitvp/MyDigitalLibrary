# Wishlist free-form editing (cover upload, edition fields, genres), 161-entry language/category backfill

**Date:** 2026-09-14
**Context:** The owner pointed at a screenshot of the wishlist edit page
("Искам я") and said two things: the cover-attachment flow there (search by
ISBN/link) rarely finds anything even when they can see the cover
themselves, and unlike an owned book in "Библиотека" they had no way to fix
the wish's category, language, or other edition details — they wanted "the
same freedom as with books I own." After that was built and verified, the
owner separately noticed most of the wishlist still had no language or
category tag and asked me to find and apply what I could, leaving whatever
I couldn't confidently determine for them to fix by hand.
**Status:** Complete. UI change verified end-to-end with a scripted
Playwright run against the real dev stack (login → create a throwaway
wishlist entry → upload a cover file with no edition yet → pick a genre →
edit ISBN/publisher/language/translator → reload to confirm persistence →
re-upload a cover now that an edition exists → delete the entry), zero
console/page errors. The 161-entry data backfill ran directly against the
live dev database through the same REST API the UI uses (no direct SQL) and
was re-verified afterward by re-fetching the full wishlist.
**Commits:** this session's own — the five-file UI diff below plus this
history entry. The 161-entry language/category fill is live `UPDATE`
traffic through the API, not a migration; nothing to commit for it besides
this doc.

## What we built

### 1. Cover attachment: replace ISBN/link search with a direct file upload

The wishlist edit page's only way to attach a cover was
`ImportApiService.lookup()` against a typed ISBN or URL — the same
"search-by-identifier" flow the import-by-link feature uses, repurposed
here even though the owner already has the image in front of them and just
wants to upload it. Library's book-detail page solves this with a plain
`<input type="file" accept="image/jpeg,image/png,image/webp">` posted to
`POST /api/v1/editions/{id}/cover`; copied that pattern verbatim (markup,
`.cover-upload` dashed-border styling, `image/jpeg,image/png,image/webp`
accept list) into the wishlist edit page in place of the old search UI.

The wrinkle Library doesn't have: a `LibraryItem` always has an edition to
upload a cover onto, but a bare `WishlistEntry` may not — `PreferredEditionId`
is nullable precisely so a want can exist as just a title/author (plan
intent, see [[project-overview]]). Added `ensureEditionId()`, which returns
the entry's existing edition id if there is one, or — on first cover upload
or first edition-details save — creates a blank `Edition` via the same
`POST /api/v1/works/{id}/editions` the "attach cover by search" flow already
used, then `PUT`s the wishlist entry's `preferredEditionId` to point at it,
all before the actual cover/edit request goes out. The UI never surfaces
this — from the owner's side, uploading a cover for a bare wish just works.

### 2. Genre (category) editing — the picker Library already has

Added `app-genre-picker` (the shared checkbox-list component, already used
on both the library edit and detail pages) to the wishlist edit page's
"Произведение" section, wired to `catalog.updateWork()` exactly the way
`library-edit.page.ts` does — read the work, keep every field but swap
`genreNames` for the picker's current selection. No backend change needed;
`Work.GenreIds` and the resolve-or-create-by-name genre pattern were already
generic across library items and wishlist entries.

### 3. A new "Издание" (edition) section — language plus everything else Library edits

Mirrored `library-edit.page.ts`'s `editionForm` wholesale: ISBN (hidden for
audiobooks), publisher, **language** (bg/en select — the field the owner
specifically asked for), translator, publication year, page count, cover
type (physical only), narrator/duration (audiobook only). `format()` is a
computed signal — `editionResource.value()?.format ?? entry.desiredFormat`
— since a bare wish has no edition to read a format from yet, but still
needs to know which fields to show. `saveEdition()` either `PUT`s the
existing edition or lazily creates one via the same `ensureEditionId()`
path as the cover upload, so filling in "Издание" fields on a bare wish
before ever touching the cover also works.

No backend changes were needed anywhere in this piece — `CreateEditionAsync`
already accepts an edition with every field null, `UpdateEditionAsync`
already validates ISBN duplicates the same way for wishlist-created and
library-created editions, and `WishlistEntryDto` already carried
`Language`/`GenreNames`/`CoverType` end-to-end (added in an earlier
session, per [[project-overview]]) — this was purely wiring the existing
Angular editing pattern onto the existing API surface, plus one dropped
translation key pair (`wishlist.form.coverSearchHint`/`useCover`, now
unused).

### 4. Verifying it — including a self-caught false alarm

Installed Playwright + Chromium fresh into the session scratch directory
(neither was present in the project or globally) and scripted the full
flow above against the actual `ng serve`/API/Postgres dev stack, using the
seed admin credentials from `.env`. First pass used a real ISBN
(`9780441013593`, the app's own placeholder text — happens to be *Dune*'s)
as edition test data and got a `409 antiforgery`-unrelated `edition.duplicate`
conflict; re-read `EditionService.UpdateAsync`/`CreateEditionAsync` to
confirm both paths run the identical duplicate-ISBN guard, concluded the
409 was the backend correctly rejecting a real collision from bad test
data rather than a bug, and re-ran with a harmless made-up value instead —
clean pass, zero console errors, cover/genre/edition fields all persisted
after a full page reload.

### 5. The 161-entry language/category backfill

Dumped the full wishlist (`GET /api/v1/wishlist`, 161 entries) and the
genre taxonomy (`GET /api/v1/genres` — only three genres exist system-wide:
*Научна литература*, *Политика и история*, *Художествена литература*) via
an authenticated Playwright session, then cross-checked a sample against
already-owned library items to confirm the existing categorization
convention (e.g. software-engineering titles like *Software Estimation*
currently carry no genre in the library either — there's no dedicated
"tech" bucket, so software books were folded into *Научна литература*
alongside the physics/biology/psychology titles already there).

Classified all 161 titles by hand from author/title knowledge — not a web
lookup pass — into `{language, genre}`, keyed to script-detection for
language (Cyrillic title → `bg`, Latin → `en`, both fully reliable across
all 161) and to genre by recognizing the specific author/subject (Dawkins/
Sagan/Pinker/Ridley/Kaku/Greene/Krauss → *Научна литература*; Litvinenko/
Pacepa/Politkovskaya/Browder/Bulgarian-communism histories → *Политика и
история*; a handful of actual novels → *Художествена литература*).
Deliberately left 7 titles with no genre rather than guess: two duplicate
*The World According to Cunk* entries (comedic pseudo-nonfiction, doesn't
cleanly fit any of the three buckets), and five Bulgarian titles whose
content/author I wasn't confident enough about (*Повече ще те няма*,
*Преподреждането на обществото* — no author on file, *Първата българска
готварска книга* — a historical cookbook, *Семиотика и критика на
културата* — humanities/semiotics, *Фауда* — uncertain attribution).

Applied through the same REST endpoints the UI now uses (`PUT
/api/v1/works/{id}` for genre, `PUT`/`POST /api/v1/editions` +
`PUT /api/v1/wishlist/{id}` for language on bare wishes) via a batch script
driving an authenticated Playwright page — chosen over raw SQL specifically
so every resolve-or-create-genre rule and the lazy-edition-creation path
just built in step 1 both ran for real, not a shortcut around them.
Discovered along the way that mutating endpoints require a fresh
`X-XSRF-TOKEN` header from `GET /api/v1/auth/antiforgery` (Angular's
`antiforgeryInterceptor` does this transparently; a plain script has to
replicate it explicitly).

First full run crashed 98 entries in on a transient `Failed to fetch` —
the genre-update code path had no `try/catch` around it (only the
language path did), so one flaky request took down the whole batch.
Fixed by wrapping both paths in `try/catch` plus a 3-attempt retry with
backoff, then re-ran from scratch: since every check is "skip if the field
is already set," the second run simply picked up wherever the first had
gotten to, at no risk of double-writing. Final state, re-verified by
re-fetching the wishlist: **0 of 161 entries missing a language, 7 of 161
still missing a genre** (the ones deliberately left above).

## Why we built it this way

**"The same freedom as with books I own" meant matching Library's actual
editing surface, not inventing a wishlist-specific one.** Every field
added — cover upload, genre picker, the whole edition form — is a direct
copy of an existing, working Library pattern (same component, same request
shape, same i18n keys where they already fit). The only genuinely new
logic is `ensureEditionId()`, and that exists solely to paper over the one
real structural difference: a `LibraryItem` always has an edition, a
`WishlistEntry` might not.

**A `409` isn't automatically a bug just because it fires during your own
manual test.** Reading the actual validation code before assuming the new
`saveEdition()` path was broken caught that the test data (a real, already-
used ISBN) was the problem, not the feature — the same conflict would have
fired identically through Library's own edit form.

**Only apply a classification you're actually confident about, and say
exactly why the rest were skipped.** The owner explicitly invited "whatever
remains, I'll fix by hand" — so accuracy on the ~152/136 confident calls
mattered more than forcing all 161 to have *some* value. Titles landed in
the "skip" pile for concrete, statable reasons (no author on file, genre
doesn't fit the 3-bucket taxonomy, uncertain identity) rather than being
silently guessed.

**A crash mid-batch is a reason to make the retry logic uniform, not to
re-run and hope.** The fix wasn't "add a try/catch to the one path that
broke" — both the genre and language code paths needed the same
try/catch-and-continue treatment, since either could hit the same class of
transient network failure on any future re-run.

## Data changes (live, not in git — no schema change involved)

- **Language** set on 136 of 161 wishlist entries (25 already had one);
  **0 entries now missing a language**. Every fill either updated an
  existing `Edition.Language` or, for bare wishes, created a new blank
  `Edition` (format = the entry's desired format, every other field null)
  and pointed the entry's `PreferredEditionId` at it.
- **Genre** set on 152 of 161 entries via `Work.GenreIds` (2 already had
  one, one genre each — no entry given more than one genre); **7 entries
  intentionally left without one** (listed above).
- No new duplicate `Work`/`Edition` rows: language fills reused an existing
  edition wherever `PreferredEditionId` was already set (most entries, per
  the cover-type backfill two sessions ago) and only created a new blank
  one for entries that had none at all.

## Files created/changed

- `src/web/src/app/features/wishlist/wishlist-edit/wishlist-edit.page.ts` —
  cover file upload (`onCoverSelected`/`ensureEditionId`/`linkEdition`)
  replacing ISBN/link search (`searchCover`/`useCoverCandidate` removed);
  `selectedGenres` + `onGenresChange` for the genre picker; new
  `editionForm`/`editionResource`/`format` computed signal and
  `saveEdition()` for the edition-details section.
- `src/web/src/app/features/wishlist/wishlist-edit/wishlist-edit.page.html` —
  cover section now a `.cover-upload` file input instead of a search box;
  `app-genre-picker` added to "Произведение"; new "Издание" section
  (ISBN/publisher/language/translator/year/pages/cover type/narrator/
  duration) copied from `library-edit.page.html`.
- `src/web/src/app/features/wishlist/wishlist-edit/wishlist-edit.page.scss` —
  swapped `.cover-search`/`.cover-search-row`/`.candidate` styles for the
  single `.cover-upload` style already used on the library detail page.
- `src/web/public/assets/i18n/bg.json`, `en.json` — dropped the now-unused
  `wishlist.form.coverSearchHint`/`useCover` keys; every other label reused
  existing `library.form.*`/`library.coverType.*` keys, so no new strings.

## What's still open

- The 7 skipped titles (*The World According to Cunk* ×2, *Повече ще те
  няма*, *Преподреждането на обществото*, *Първата българска готварска
  книга*, *Семиотика и критика на културата*, *Фауда*) still have no
  category — left for the owner, per their own instruction.
- The two *The World According to Cunk* entries look like a duplicate wish
  (same title/author, both already `en`) — not confirmed against the
  earlier cover-type backfill's note that this title was deliberately
  wishlisted in both paperback and hardback, so left alone rather than
  assumed to be an accidental dupe.
- No backend change was needed for any of this, so nothing is queued
  there; the genre taxonomy still has only three buckets system-wide,
  which is why several borderline titles (business/corporate history,
  humor-as-nonfiction, general humanities) got folded into the closest
  existing bucket rather than a dedicated one.

## Next

Nothing queued. Waiting on the owner to review the wishlist edit page's
new cover-upload/genre/edition-details sections in normal use, and to
manually finish categorizing the 7 titles intentionally left blank.
