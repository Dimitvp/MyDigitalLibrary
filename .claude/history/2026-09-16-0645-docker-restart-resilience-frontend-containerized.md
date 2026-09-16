# Docker restart resilience, and containerizing the frontend

**Date:** 2026-09-16
**Context:** The owner restarted Docker Desktop; afterwards `mydigitallibrary-api-1`
was stuck `Exited (139)` and `localhost:4301/wishlist` refused to connect. Asked
to diagnose both the API crash and the site not loading, and separately whether
the local database backups were still intact. Once fixed, asked how to stop this
recurring — specifically floated "add it to docker-compose or something" for the
frontend, which until this session was only ever started by hand (`npm start`)
and had drifted onto ad hoc/zombie ports across past sessions. Finished by asking
how the same problem is solved in the owner's other local project, `svara-bg`,
which led to redoing the frontend container as an nginx production build instead
of a dev server, matching that project's pattern.
**Status:** Complete. Stack verified end-to-end after every change; nothing
queued.
**Commits:** this session's commit (see `git log`).

## 1. Diagnosing the post-restart failures

**API container `Exited (139)`:** logs showed `Npgsql.NpgsqlException` →
`SocketException: Name or service not known` resolving the `db` hostname —
DNS for the `db` service wasn't up yet when `api` tried to connect and run
migrations. Root cause: on a Docker Desktop restart, containers come back up
per their own `restart` policy, not in `depends_on` order (that ordering is
only honored by `docker compose up`). `db-backup` already had
`restart: unless-stopped`; `api` had none, so after the one-shot DNS-timing
crash nothing brought it back. Fixed by starting it manually, then adding
`restart: unless-stopped` to `api` in `docker-compose.yml`.

**Backups:** all present and correct — 30 dump files, event-driven (change
detected via Postgres `LISTEN`/`NOTIFY` on content tables, debounced, plus a
7-day ceiling), last successful one ~17h old at the time, which is expected
for that trigger model, not a gap. One failed attempt was visible in
`db-backup`'s logs exactly during the restart window (`pg_dump: error: could
not open output file ... I/O error` — the bind-mounted `./db-backup` was
transiently unavailable during the host/Docker restart); `db-backup` has
`restart: unless-stopped` too, so it came back up on its own, reinstalled its
change triggers, and resumed watching. No data loss, no manual fix needed.

**`localhost:4301` refused to connect:** unrelated second issue. The frontend
was never part of `docker-compose.yml` — it only ever ran via a manually
started `npm start`, and wasn't running at all post-restart (no `ng serve`
process; only an unrelated Adobe Creative Cloud `node.exe` showed up in a
`node.exe` process scan). Also the port was simply wrong: `.claude/history/
2026-09-13-1550-...port-fix.md` had already pinned the dev server to **4201**
in `angular.json` specifically because 4200 collides with `svara-frontend`'s
Docker-published port — `4301` was a leftover ad hoc port from a since-killed
zombie process, not anything durable. Started `npm start` once to confirm
4201 was reachable, which is what led into the automation question.

## 2. Making the frontend part of the Docker stack

First pass (superseded by §3): containerized `web` as a **dev-mode** service —
`node:22-bookworm-slim` image running `ng serve` for live-reload, `src/web`
bind-mounted with `node_modules` kept in an anonymous volume (so the Linux
container never inherits the host's Windows-specific native binaries, e.g.
`@rolldown/binding-win32-x64-msvc`), a separate `proxy.docker.conf.json`
(`api:8080`, the in-network hostname) selected via a new `docker` configuration
in `angular.json`'s `serve` target, `CHOKIDAR_USEPOLLING=true` for file-watch
reliability over a Windows bind mount. Verified working (`curl localhost:4201/
wishlist` → 200, proxied `/health/ready` → 200) before the owner asked how
`svara-bg` handles this.

## 3. Switching to nginx production build, matching `svara-bg`

`svara-bg`'s `src/Frontend/Dockerfile` builds `ng build --configuration
production` in a `node` stage, then serves the static output from an
`nginx:1.27-alpine` stage — no dev server, no bind mount, no hot-reload; a
code change needs a rebuild. Its `nginx.conf` reverse-proxies API paths to
each backend service by container hostname and falls back to `index.html`
for every other path (Angular client-side routing). The owner chose this
over the dev-server approach for the more stable production-like footprint,
even knowing it costs hot-reload in Docker.

Replaced the dev-mode `web` service with the same shape:
- `src/web/Dockerfile`: `node:22-bookworm-slim` build stage (`npm ci`, `npm
  run build -- --configuration production`) → `nginx:1.27-alpine` runtime
  stage serving `dist/web/browser`.
- `src/web/nginx.conf`: proxies `/api/`, `/health/`, `/covers/` to
  `http://api:8080`; `location /` does `try_files $uri $uri/ /index.html`;
  1-year cache headers on hashed static assets, no-cache on `index.html`.
- `src/web/.dockerignore` (`node_modules`, `dist`, `.angular`) — without it
  the build context was 240MB and took ~50s to transfer on every build.
- Reverted the dev-server detour: removed `proxy.docker.conf.json` and the
  `docker` `serve` configuration from `angular.json` (dead once nothing runs
  `ng serve` in Docker), dropped the bind mount / anonymous volume / polling
  env var from `docker-compose.yml`.
- `docker-compose.yml`: `web` now just `build: ./src/web`, `depends_on: [api]`,
  `restart: unless-stopped`. `docker-compose.override.yml`: port publish
  changed from `4201:4201` to `4201:80` (nginx listens on 80 internally).

Verified: `docker compose config --quiet` clean; full `docker compose up -d
--build web` rebuild; `curl` 200s on `/`, `/wishlist` (SPA fallback),
`/health/ready` (proxied), a static asset; nginx access log confirms real
requests being served through the proxy.

**Local dev workflow going forward:** `docker compose up -d` is sufficient
for normal use — no hot-reload, but self-heals through restarts.
For active frontend work, run `npm start` locally instead (uses the existing
`proxy.conf.json` → `localhost:8081`) — just never at the same time as the
Docker `web` container, both bind host port 4201.

## Why we built it this way

**Fix the setting, not just the moment (again — see the 2026-09-13 port-fix
entry for the same lesson applied to the port).** The API crash would have
recurred on every future Docker Desktop restart if the fix had stopped at
"start it again manually" — `restart: unless-stopped` on `api` (mirroring
what `db-backup` already had) closes the actual gap.

**A bare `depends_on` doesn't survive an engine-level restart.** Compose only
honors dependency *ordering* when it's the one issuing the start command
(`docker compose up`); a Docker Desktop/host restart brings containers back
independently per their own restart policy. The fix for "service A must be
up before service B" across *any* kind of restart is a restart policy on
every service that should self-heal, not just correct `depends_on` wiring.

**Matched the sibling project's approach deliberately, not by default.**
Dev-server-in-Docker (hot-reload, heavier, some Windows bind-mount file-watch
risk) and nginx-production-build (stable, zero hot-reload) are both
legitimate; asked the owner explicitly rather than picking one, since it's a
recurring day-to-day workflow trade-off only they can weigh for how actively
this project is being developed right now.

## Files changed

- `docker-compose.yml` — `restart: unless-stopped` on `api`; new `web`
  service (`build: ./src/web`, `depends_on: [api]`, `restart: unless-stopped`)
- `docker-compose.override.yml` — `web.ports: ["4201:80"]`
- `src/web/Dockerfile` (new) — multi-stage `node` build → `nginx` runtime
- `src/web/nginx.conf` (new) — API reverse proxy + SPA fallback + cache headers
- `src/web/.dockerignore` (new) — excludes `node_modules`, `dist`, `.angular`
- `README.md` — Frontend/Docker sections updated for the containerized `web`
  service, the no-hot-reload trade-off, and the "don't run both" port warning
- `src/web/README.md` — same note, scoped to the Angular project's own README

## What's still open

Nothing queued. `docker compose up -d` from the repo root is now the whole
local dev stack (db, db-backup, api, web), self-healing across restarts.
