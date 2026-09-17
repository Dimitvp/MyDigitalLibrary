# Responsive header nav — hamburger collapse on phones

**Date:** 2026-09-17
**Context:** With the app now live and reachable from anywhere, the owner
opened `biblioteka.svara.bg` on a phone browser and found the header nav
bar overflowing off the right edge — the shell header (brand + 4 nav links
+ language switch + logout button) was a bare `display: flex` row with no
wrap or collapse handling at all, so anything narrower than roughly 780px
just ran off-screen instead of adapting.
**Status:** Complete. Verified visually (not just built-and-assumed) with a
headless Playwright pass at 390px (closed and open hamburger states) and
1280px (confirming no desktop regression), then deployed to production.
**Commits:** this session's commit (see `git log`).

## What changed

Below 780px, the header now shows a hamburger/✕ toggle button (inline SVG,
no icon library dependency) instead of the nav+actions row. Tapping it
opens a dropdown panel — nav links, language switch, logout — stacked
below the header (`position: absolute`, matching the app's `--shadow`
token). The whole panel closes itself on any tap inside, via one `(click)`
handler on the container relying on event bubbling, rather than wiring a
close call onto every individual link/button — so it never lingers open
across a route change. Above 780px the layout is untouched: same flex row
as before.

Checked the rest of the app for the same class of gap first (grep for
`flex-wrap`/`@media` in the two main list pages) — both `library-list` and
`wishlist-list`'s filter/sort rows already wrap, and `library-detail`
already collapses its two-column layout under 560px from an earlier
session. The header was the one place with zero responsive handling at
all, not a symptom of a broader missing pattern.

## Why we built it this way

**Looked, didn't just built-and-assumed.** No dedicated screenshot/browser
tool existed in this environment for this project — found Playwright
already cached locally (`npx --no-install playwright --version` resolved),
`npm install`ed it into the scratchpad to get an importable module, and
drove a real headless Chromium through login → phone-width screenshot →
tap the hamburger → screenshot → a separate desktop-width pass, before
calling this done. A CSS-only "should work" fix for a visual bug report
is exactly the kind of change that's cheap to verify and expensive to get
subtly wrong (this session's UI work — even other Angular-focused sessions
this week — never had a working screenshot loop until this one).

## Files changed

- `src/web/src/app/app.html` — hamburger toggle button, collapsible nav+actions wrapper
- `src/web/src/app/app.ts` — `menuOpen` signal, `toggleMenu()`/`closeMenu()`
- `src/web/src/app/app.scss` — `$shell-collapse-breakpoint: 780px`, the collapsed/open dropdown styles
- `src/web/public/assets/i18n/bg.json`, `en.json` — `nav.openMenu`/`nav.closeMenu`

## What's still open

Nothing queued from this specific report. If other pages turn out to have
similar narrow-width issues the owner hasn't hit yet, they'll surface the
same way this one did — worth a proactive phone-width pass with the same
Playwright approach at some point, but not chased today beyond the header
that was actually reported broken.
