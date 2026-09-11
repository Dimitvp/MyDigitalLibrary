# Stage 5 — Angular skeleton

**Date:** 2026-09-11
**Stage:** 5 (Angular skeleton)
**Status:** Complete
**Commits:** `12ca501`, `e0b1ab0`, `7d0d19e`, `8d89357`

## What we built

The plan's Stage 5 scope, no more: shell, routing, i18n (bg/en), a login
screen, and library list + detail + add-book pages. Explicitly out of scope
and not touched — statistics, charts, dark theme.

- **Shell** (`app.ts`/`.html`/`.scss`): nav bar, bg/en language switcher,
  logout button, `<app-notification />` outlet, `<router-outlet />`. Replaces
  the Angular CLI's default marketing-page scaffold entirely.
- **Routing** (`app.routes.ts`, `features/library/library.routes.ts`):
  `/` redirects to `/library`; `/login` is lazy-loaded and public;
  `/library`, `/library/add`, `/library/:id` are lazy-loaded and
  `authGuard`-protected; unknown paths redirect to `/library`.
- **Auth**: `AuthService` (signal-based `currentUser`, `undefined` until the
  first `/auth/me` check resolves vs. `null` once checked-and-logged-out —
  so the guarded shell never flashes a logged-out state before the check
  lands), `authGuard` (a `CanActivateFn` that reuses a resolved
  `currentUser` or calls `/auth/me` once and redirects to `/login` with a
  `returnUrl` on failure), `AntiforgeryService` + a functional interceptor
  that attaches `X-XSRF-TOKEN` to mutating requests, mirroring the dance the
  Stage 4 integration test already exercises server-side.
- **i18n**: `@jsverse/transloco`, `TranslocoHttpLoader` fetching
  `/assets/i18n/{lang}.json`, `LanguageService` (bg default, choice
  persisted to `localStorage`). Full bg/en translation trees cover nav,
  auth, the library list/detail/form, and every backend `errorCode` the API
  can currently return, consumed by an error interceptor that turns
  ProblemDetails responses into translated notifications.
- **Pages**: `LoginPage` (typed reactive form); `LibraryListPage` /
  `LibraryDetailPage` (signal-based `httpResource` reads against the
  already-enriched `LibraryItemDto`); `LibraryFormPage` (typed reactive
  form building the nested `work`/`edition` composite-create payload per
  plan section 4.1, posted via `LibraryApiService`).
- **Dev-server proxy** (`proxy.conf.json`): forwards `/api` and `/health` to
  the API container so the SPA is same-origin to the backend in dev —
  no CORS, cookie auth works without `withCredentials`.

## Why we built it this way

### Verified by real execution, not just `ng build`

Every earlier stage's rule applied here too. After `ng build` and `ng test`
passed, brought up the real `docker compose` stack (Postgres + API,
seeded admin), ran `ng serve` against it through `proxy.conf.json`, and
drove the actual HTTP sequence a browser session would make:
`GET /auth/antiforgery` → `POST /auth/login` (with the antiforgery header)
→ `GET /auth/me` → `GET /library-items` — all through port 4300 (the dev
server), not straight to the API port, so the proxy path itself was under
test. Got back the real seeded Dune/Foundation library items. Also checked
the negative cases: `/auth/me` without a session returns 401 (the input
`authGuard` redirects on), and an unmapped client route (`/some/nonexistent
/path`) still serves `index.html` with a 200 rather than a server 404,
since Angular's router — not the server — owns unknown-route handling.
Stack torn down (`docker compose down -v`) and scratch files removed
afterward, same as every prior stage.

### `currentUser: undefined | null | CurrentUser`, not a boolean

A plain `isLoggedIn` boolean can't distinguish "haven't checked yet" from
"checked, not logged in" — collapsing them would make the guarded shell
briefly render as logged-out on every hard refresh while the first
`/auth/me` call is in flight. `undefined` (unchecked) vs. `null` (checked,
anonymous) vs. a real user keeps `authGuard` and the shell's logout-button
visibility correct without a loading-state signal bolted on separately.

### `httpResource` for reads, not a manual `subscribe` + signal pair

Angular 22's `httpResource` (confirmed stable, `@publicApi 22.0`, from
earlier verification in this project) gives list/detail pages a reactive
GET tied to route params with built-in loading/error state, matching the
plan's instruction to lean on the framework's current idioms rather than
hand-rolling what it already provides. Mutating calls (login, create) still
go through explicit `HttpClient` + `subscribe`, since `httpResource` is a
read-oriented primitive.

### The composite-create contract, not three separate calls

`LibraryFormPage` builds one `CreateLibraryItemRequest` with nested
`work`/`edition` objects and posts it once to `POST /library-items` —
mirroring the backend's Stage 3 composite-creation design (plan section
4.1: no `POST /works`/`POST /editions`, one transactional composite
endpoint) rather than reintroducing a three-request client-side dance the
backend was specifically built to avoid.

### `ng test` needed rewriting, not just leaving broken

The CLI-scaffolded `app.spec.ts` asserted on the default template's
`<h1>Hello, web</h1>`, which no longer exists once the shell was replaced.
Rewrote it against the real `App` component: provides `HttpClientTesting`,
`provideRouter([])`, and a real `provideTransloco` (backed by
`TranslocoHttpLoader`, with the translation file request flushed through
`HttpTestingController` rather than mocked away) so the test exercises the
actual i18n bootstrap path and asserts the shell renders the translated
brand title and nav link — not just "it instantiates."

### Alternate dev-server port for verification only

Port 4200 was already in use by another process on this machine at
verification time; `ng serve --port 4300` was used only for the manual
end-to-end check and isn't persisted anywhere in committed config —
`npm start` still runs the default port for normal use.

## Files created

- `src/web/proxy.conf.json`
- `src/web/src/app/core/api/models.ts`
- `src/web/src/app/core/auth/{antiforgery.service,auth.guard,auth.service}.ts`
- `src/web/src/app/core/http/{antiforgery.interceptor,error.interceptor}.ts`
- `src/web/src/app/core/i18n/{language.service,transloco-http-loader}.ts`
- `src/web/src/app/shared/ui/notification/{notification.service,notification}.ts`
- `src/web/src/assets/i18n/{bg,en}.json`
- `src/web/src/app/features/auth/login/login.page.{ts,html,scss}`
- `src/web/src/app/features/library/library-api.service.ts`
- `src/web/src/app/features/library/library.routes.ts`
- `src/web/src/app/features/library/library-list/library-list.page.{ts,html,scss}`
- `src/web/src/app/features/library/library-detail/library-detail.page.{ts,html,scss}`
- `src/web/src/app/features/library/library-form/library-form.page.{ts,html,scss}`
- Rewritten: `src/web/src/app/app.{ts,html,scss,routes.ts,config.ts,spec.ts}`, `src/web/src/index.html`

## What's still open

- No wishlist UI yet — `nav.wishlist` translation keys exist but are unused;
  the wishlist feature area itself is a later stage, not Stage 5 scope.
- `LibraryDetailPage` has delete (confirmed wired to the real
  `DELETE /library-items/{id}` endpoint) but no edit flow yet, even though
  `PUT /library-items/{id}` exists backend-side.
- `library-form` currently only supports the nested work+edition
  composite-create path (`editionId: null`) — there's no "attach to an
  existing edition" UI yet, even though the backend DTO supports it.
- No component-level tests beyond the rewritten `app.spec.ts` — list/detail/
  form pages have no specs yet.

## Next

Stage 6 per the plan — continue only once the user confirms this history
entry is in and pushed.
