# Pre-deploy security hardening — login brute-force, forwarded headers, prod seeding

**Date:** 2026-09-17
**Context:** The owner wants to put MyDigitalLibrary on the same Hetzner box
already hosting svara.bg (confirmed comfortable headroom: 5.2GiB RAM free of
7.6GiB, 48GB disk free of 75GB — see conversation), reachable at a free
subdomain of `svara.bg` rather than buying a new domain. Before touching the
live server, explicitly asked for a security pass first: "ако случаен човек
попадне на този поддомейн да няма възможност да хакне сайта" (if a random
person lands on this subdomain, they shouldn't be able to hack the site) —
this app has never been exposed to the internet before, only ever run on
localhost.
**Status:** Security fixes complete and verified (248/248 tests, live
smoke-checked against the running dev stack). Deploy files (docker-compose.prod.yml,
nginx subdomain config) are the next step, not started yet.
**Commits:** this session's commit (see `git log`).

## What got checked

Read `Program.cs` end to end, every `Endpoints/*.cs` file's authorization
wiring, `AuthEndpoints.cs`, `ApplicationExceptionHandler.cs`,
`AntiforgeryFilter.cs`, `IdentitySeeder.cs`, and grepped the Angular app for
`localStorage`/`sessionStorage`/`Authorization` header usage.

**Confirmed already solid, no changes needed:**
- Every API route group requires authentication (`RequireAuthorization()`) —
  checked exhaustively, the only anonymous endpoints are `POST /auth/login`
  and `GET /auth/antiforgery`, both correctly so.
- Session lives entirely in an `HttpOnly` cookie; nothing token-like sits in
  `localStorage`/`sessionStorage` on the Angular side — not exfiltratable
  via XSS the way a JS-readable token would be.
- CSRF: every state-changing endpoint carries `AntiforgeryFilter`.
- `ApplicationExceptionHandler` maps known exceptions to clean
  `ProblemDetails`; unrecognized ones fall through to the framework default,
  which doesn't leak stack traces outside Development.

## What was wrong — three real gaps, all fixed

**1. No brute-force protection on login.** `AuthEndpoints.cs` called
`PasswordSignInAsync(..., lockoutOnFailure: false)` — unlimited password
guesses against the one admin account, no rate limiting anywhere in the
pipeline either. Fixed: `lockoutOnFailure: true` (Identity's default: 5
attempts, 5-minute lockout) plus a `RequireRateLimiting("login")` fixed-window
policy (10 requests / 5 min, partitioned by client IP) as defense in depth —
lockout alone only throttles guesses against one *known* email; the IP-based
limit also blunts spraying across many.

**2. No `ForwardedHeadersMiddleware`.** The app will sit behind host-level
nginx in production (same pattern as svara.bg), which means Kestrel only ever
sees plain HTTP with the real scheme/client-IP carried in
`X-Forwarded-Proto`/`X-Forwarded-For`. Without recognizing those: the
session cookie's `SecurePolicy = CookieSecurePolicy.SameAsRequest` would
evaluate `Request.IsHttps` as `false` and omit the `Secure` flag even on a
live HTTPS site, and `UseHttpsRedirection()` would issue a pointless redirect
on every single request. This is the *exact* bug class svara.bg's own
deployment already hit and fixed (`571f28f`, documented in
`.claude/reports/2026-08-18-1809-hetzner-deploy-conversation-wrapup.md`) —
same fix applied here pre-emptively rather than rediscovering it after a live
deploy: `UseForwardedHeaders` with `KnownIPNetworks`/`KnownProxies` cleared
(not left at the loopback-only default — Docker's port-publishing NATs the
proxy's connection to the bridge gateway IP, not literal loopback, so the
default wouldn't recognize nginx as trusted; safe to trust unconditionally
here specifically because the API port will be bound to `127.0.0.1` only, so
nginx is the only thing that can ever reach it).

**3. No way to create a production admin account at all.** `IdentitySeeder`
was explicitly "Development-only" by its own doc comment, and
`db.Database.MigrateAsync()` was gated inside the same `IsDevelopment()`
block — meaning a `Production`-environment deploy would boot with no schema
and no user, and (since there's no self-registration endpoint) no way to log
in, ever. Asked the owner how they wanted this resolved rather than picking
unilaterally: keep `SEED_ADMIN_EMAIL`/`SEED_ADMIN_PASSWORD` seeding working
in every environment (their choice) vs. a one-off manual migrate/seed step
per deploy. Implemented their pick: migration + admin seeding now run in
every environment (safe as auto-migrate specifically because this is a
single, never-horizontally-scaled instance); `DevelopmentSeeder`'s
Dune/Foundation demo books stay Development-only, never seeded in
production. **The owner still needs to set a real `SEED_ADMIN_PASSWORD` in
the server's `.env` before deploying** — not the local dev default
(`ChangeMe123!`), which is public in this repo's history.

## Tests added

`tests/MyDigitalLibrary.Api.IntegrationTests/Auth/AuthEndpointsTests.cs` (new
file — no auth endpoint tests existed before): lockout after 5 failed
attempts blocks even the *correct* password on the 6th try; a normal correct
login still succeeds and the session cookie authenticates a follow-up
`/auth/me` call.

## Why we built it this way

**Fix the class of bug, not the instance.** The forwarded-headers gap wasn't
found by testing MyDigitalLibrary in production (it's never been deployed) —
it was found by recognizing "this app is about to sit behind the exact same
nginx-reverse-proxy topology svara.bg already hit this exact bug on" and
checking for it pre-emptively, rather than waiting to rediscover it via a
broken login after the subdomain goes live.

**Lockout and rate limiting are complementary, not redundant.** Per-account
lockout (Identity's own mechanism) does nothing against an attacker guessing
across many different email addresses; per-IP rate limiting does nothing
against a slow, patient attacker who never crosses the rate-limit window
against one account. Both were missing; both are now in place, each covering
what the other doesn't.

**Ask before picking a production seeding strategy, don't guess.** Both
"seed always" and "migrate manually per deploy" are legitimate, defensible
choices with real tradeoffs (convenience vs. never auto-running schema
changes against production data) — this is an operational preference about
how the owner wants to run their own server, not a correctness question with
one right answer, so it went to them rather than being decided silently.

## Files changed

- `src/MyDigitalLibrary.Api/Program.cs` — `UseForwardedHeaders`, rate
  limiter registration + login policy, migrate/seed moved out of the
  `IsDevelopment()` gate (demo-book seeding stays gated)
- `src/MyDigitalLibrary.Api/Endpoints/AuthEndpoints.cs` — `lockoutOnFailure: true`,
  `RequireRateLimiting("login")`
- `src/MyDigitalLibrary.Infrastructure/Auth/IdentitySeeder.cs` — doc comment
  updated (no longer Development-only)
- `tests/MyDigitalLibrary.Api.IntegrationTests/Auth/AuthEndpointsTests.cs` (new)

## What's still open

- **Before the actual deploy**: set a real, strong `SEED_ADMIN_PASSWORD` in
  the server's `.env.production` (or equivalent) — not the local dev
  default.
- Deploy files not started yet: `docker-compose.prod.yml` for
  MyDigitalLibrary (loopback-bound ports, mirroring svara-bg's SEC-H3
  pattern), a new host-nginx `server{}` block + `certbot --nginx` for the
  chosen subdomain (owner leaning toward something under `svara.bg`, exact
  name not yet chosen).
- Noted but not addressed (optional hardening, not blockers): no explicit
  HSTS header, no Content-Security-Policy — worth a pass at some point but
  didn't come up as part of the "can a random visitor break in" threat model
  this round focused on.
