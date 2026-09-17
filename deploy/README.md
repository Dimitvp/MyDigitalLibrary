# Deploying to biblioteka.svara.bg

Runs on the same Hetzner box as svara.bg (`svara-prod`, `46.62.253.87`) —
confirmed comfortable headroom (5.2GiB RAM free / 7.6GiB, 48GB disk free /
75GB) as of 2026-09-17, no server upgrade needed. See
`.claude/history/2026-09-17-1400-pre-deploy-security-hardening.md` for the
security pass this app went through before being exposed to the internet for
the first time — read that first if anything here seems unfamiliar.

## One-time server setup

1. **DNS** — in the Hetzner DNS console, same zone as svara.bg: add an `A`
   record `biblioteka` → `46.62.253.87`.
2. **Directory + secrets:**
   ```bash
   ssh root@46.62.253.87 'mkdir -p /opt/biblioteka'
   scp .env.production.example root@46.62.253.87:/opt/biblioteka/.env.production
   ssh root@46.62.253.87 'chmod 600 /opt/biblioteka/.env.production'
   # then edit /opt/biblioteka/.env.production on the server directly (nano/vim)
   # and fill in real values — especially SEED_ADMIN_PASSWORD, which must NOT
   # be the local-dev default ("ChangeMe123!" is public in this repo's history).
   ```
3. **Host nginx:**
   ```bash
   scp deploy/nginx/biblioteka.svara.bg.conf root@46.62.253.87:/etc/nginx/sites-available/
   ssh root@46.62.253.87 '
     ln -sf /etc/nginx/sites-available/biblioteka.svara.bg.conf /etc/nginx/sites-enabled/ &&
     nginx -t && systemctl reload nginx
   '
   ```
4. **First deploy** (below), then **TLS** once DNS has propagated and the
   site answers on plain HTTP:
   ```bash
   ssh root@46.62.253.87 'certbot --nginx -d biblioteka.svara.bg'
   ```

## Deploying (every time)

Same manual pattern as svara.bg until/unless this gets a CI/CD workflow of
its own (not set up yet — ask if you want one):

```bash
# from a local clone, after committing
git archive --format=tar HEAD -o /tmp/biblioteka-deploy.tar
scp /tmp/biblioteka-deploy.tar root@46.62.253.87:/tmp/
ssh root@46.62.253.87 'mkdir -p /opt/biblioteka && tar -xf /tmp/biblioteka-deploy.tar -C /opt/biblioteka && rm /tmp/biblioteka-deploy.tar'

ssh root@46.62.253.87 '
  cd /opt/biblioteka &&
  docker compose -f docker-compose.prod.yml --env-file .env.production up -d --build
'
```

Per-service rebuild (e.g. after a frontend-only change):
```bash
ssh root@46.62.253.87 '
  cd /opt/biblioteka &&
  docker compose -f docker-compose.prod.yml --env-file .env.production up -d --build --no-deps web
'
```

## What's different from local dev

- `ASPNETCORE_ENVIRONMENT=Production` (local dev uses `Development`) —
  migrations and admin seeding still run automatically either way (see the
  security-hardening history entry linked above), but `/openapi` and the dev
  demo-book seeder are Development-only and stay off here.
- Every container port is bound to `127.0.0.1` — host nginx is the only
  public entry point, same SEC-H3 pattern as svara-bg.
- `docker-compose.override.yml` (local-only port publishing) is never used
  here — `docker-compose.prod.yml` is a single, complete, hand-written file,
  same reasoning as svara-bg's own prod compose file (see its header comment
  and `.claude/reports/2026-08-12-1338-hetzner-deployment-postmortem.md` in
  that repo — this exact host has a reproducible container-networking bug
  when a compose project is assembled from multiple `-f` files).

## Operational notes carried over from svara.bg's own deploy (same server)

- First container of a `docker compose up -d` batch sometimes hits a
  benign `address already in use` port-bind race — just retry, it's
  idempotent.
- SSH sessions drop intermittently under background internet scanning
  noise — for anything longer than a few seconds, prefer
  `nohup cmd > /tmp/log 2>&1 & disown` over one long foreground command.
- Hetzner's DNS nameservers are `hydrogen.ns.hetzner.com`,
  `oxygen.ns.hetzner.com`, and **`helium.ns.hetzner.de`** (not `.com`) —
  easy to typo.
