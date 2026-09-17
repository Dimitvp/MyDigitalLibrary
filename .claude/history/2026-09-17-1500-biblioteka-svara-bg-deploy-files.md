# Deploy files for biblioteka.svara.bg

**Date:** 2026-09-17
**Context:** Follow-up to the security hardening pass earlier the same day.
With the app confirmed safe to expose (login lockout, forwarded headers,
production seeding all fixed and verified), prepared the actual files to
deploy MyDigitalLibrary onto the same Hetzner box already hosting svara.bg,
reachable at a free subdomain (`biblioteka.svara.bg`, chosen from three
options offered) rather than a new domain.
**Status:** Files written, validated locally (config syntax + a full local
Production-mode smoke test), not yet copied to the actual server — that's
the owner's call to make (SSH access, DNS change, real secrets).
**Commits:** this session's commit (see `git log`).

## What got created

- **`docker-compose.prod.yml`** — single hand-written file (not `-f base -f
  override`), modeled directly on svara-bg's own `docker-compose.prod.yml`
  including its exact warning-comment about why: this same host has a
  reproducible container-networking bug when a compose project is assembled
  from multiple `-f` files (root-caused in svara-bg's own
  `.claude/reports/2026-08-12-1338-hetzner-deployment-postmortem.md`).
  Every port bound to `127.0.0.1` only (db 5532, api 8081, web 4201 — same
  numbers as local dev, just loopback-scoped instead of open), `restart:
  always` (matching svara-bg's prod convention, not local dev's
  `unless-stopped`), `ASPNETCORE_ENVIRONMENT: Production`.
- **`deploy/nginx/biblioteka.svara.bg.conf`** — host-level nginx, one
  `server{}` block proxying everything to `127.0.0.1:4201`. Much simpler than
  svara.bg's own host config (which has a location block per backend
  service) because MyDigitalLibrary's `web` container already does its own
  internal reverse-proxying to `api` — host nginx here only ever needs to
  know about one upstream.
- **`.env.production.example`** — placeholder template (real
  `.env.production` never committed, added to `.gitignore` alongside the
  existing `.env`). Comment calls out explicitly: the real
  `SEED_ADMIN_PASSWORD` must not be the local-dev default, which is public in
  this repo's own history.
- **`deploy/README.md`** — one-time server setup (DNS record, secrets file,
  host nginx, certbot) and the manual deploy command (git archive + scp +
  ssh, same pattern as svara-bg's own until/unless this gets a CI/CD
  workflow — not built this round, wasn't asked for).

## A second forwarded-headers gap, found by actually testing the topology

`biblioteka.svara.bg` is a **two-hop** proxy chain in production: host nginx
(TLS termination) → `web` container's own internal nginx → `api`. The web
container's `nginx.conf` was setting `X-Forwarded-Proto: $scheme` — correct
for a single hop, but `$scheme` there reflects only the *immediate* hop (what
the host nginx sent it, always plain HTTP), not the original client's HTTPS
connection. Left as-is, this would have silently fed the API's
just-added `ForwardedHeadersMiddleware` the wrong scheme — the exact bug that
middleware exists to prevent, reintroduced one layer further out. Fixed with
the standard nginx idiom: a `map` that passes through an already-set
`X-Forwarded-Proto` from the outer proxy and only falls back to this
container's own `$scheme` when there's no outer proxy at all (local dev).

## Verified before calling this done

- `docker compose -f docker-compose.prod.yml --env-file <dummy> config
  --quiet` — valid syntax.
- **A full local run in actual `Production` mode** — this app has never
  booted with `ASPNETCORE_ENVIRONMENT=Production` before today. Stopped the
  local dev stack, brought up `docker-compose.prod.yml` locally under a
  separate compose project name (`biblioteka-prodtest`, its own throwaway
  volumes), and confirmed: migrations ran, the admin account got seeded,
  login succeeded, the session cookie authenticated a follow-up `/auth/me`
  call, the frontend loaded — then tore the whole throwaway stack down
  (`down -v`) and restored the normal dev stack. This is exactly the kind of
  check svara-bg's own deploy history flagged as valuable ("anything gated on
  IsDevelopment()/IsProduction() ... stays invisible until the first real
  deploy") — done here before the first real deploy, not after.

One informational item surfaced during that test, not treated as a blocker:
`No XML encryptor configured. Key {...} may be persisted to storage in
unencrypted form.` (ASP.NET Core Data Protection warning — the key file
protecting the auth cookie isn't itself encrypted at rest in the
`biblioteka-dpkeys` volume). Low incremental risk here specifically: only
root/filesystem access to the server would expose it, and anyone with that
access has already compromised everything else too. Worth a
`.ProtectKeysWithCertificate(...)` pass at some point (same idea as
svara-bg's OpenIddict certs), not before this deploy.

## Why we built it this way

**Test the actual topology, not just the code.** The two-hop
forwarded-headers gap wasn't visible from reading `Program.cs` or
`AuthEndpoints.cs` in isolation — it only showed up by tracing the literal
request path this deployment will use (host nginx → container nginx → api)
and asking "what does each hop actually see." The morning's fix (API-side
`ForwardedHeadersMiddleware`) was necessary but not sufficient on its own.

**Run it once, for real, before it's real.** A syntax-valid compose file and
a code review aren't the same as knowing the app actually boots correctly in
`Production` — the environment name gates real behavior (migrations, seeding,
`/openapi` exposure) that Development had always masked. A throwaway local
run under Production settings, torn down cleanly afterward, cost a few
minutes and caught nothing this time — but is exactly the kind of check that
would have caught the forwarded-headers class of bug on svara-bg's first real
deploy, had it been done there first instead of discovered live.

## Files changed

- `docker-compose.prod.yml` (new)
- `deploy/nginx/biblioteka.svara.bg.conf` (new)
- `deploy/README.md` (new)
- `.env.production.example` (new)
- `.gitignore` — `.env.production` ignored, `.env.production.example` kept
- `src/web/nginx.conf` — `X-Forwarded-Proto` passthrough (see above)

## What's still open

Everything in `deploy/README.md`'s "one-time server setup" — DNS record,
copying secrets to the server, installing the host nginx config, first
deploy, then `certbot`. All require the owner's own SSH/DNS access; nothing
here touches the live server yet.
