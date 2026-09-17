# biblioteka.svara.bg is live — and a real ForwardedHeaders bug caught on first deploy

**Date:** 2026-09-17
**Context:** Continuation of the same day's deploy-prep work. The owner
added the DNS record and said to continue; drove the actual deploy over SSH
(access confirmed working from this environment — same key as the owner's
own PowerShell session). Owner supplied the real Postgres and admin
passwords directly in chat.
**Status:** **Live.** `https://biblioteka.svara.bg` is up, TLS via Let's
Encrypt (auto-renewing), login verified working with the real admin
credentials, session cookie confirmed carrying the `Secure` flag correctly.
svara.bg checked and confirmed unaffected at every step.
**Commits:** this session's commits (see `git log`).

## What happened, in order

1. Confirmed SSH access works from this environment, checked svara.bg
   healthy (200) and server headroom before touching anything.
2. Wrote `/opt/biblioteka/.env.production` (chmod 600) with the owner's real
   credentials.
3. `git archive` + `scp` + `tar` to get the code onto the server, then
   `docker compose -f docker-compose.prod.yml --env-file .env.production up
   -d --build`.

### Bug #1, found immediately: `db-backup` crash-looping

`backup.sh: set: line 35: illegal option -`. Root cause: `git archive` on
this Windows dev machine (`core.autocrlf=true`) converts LF → CRLF in its
*output* for shell scripts, even though the stored git blob itself is LF
(`git show`/`cat -A` on the blob shows clean LF — the corruption happens
only in what `archive` emits). Both `docker/db-backup/backup.sh` and
`docker/db-init/10-restore-if-exists.sh` (the latter failed silently inside
Postgres's own entrypoint, `invalid option`, harmless this time only because
it was a fresh volume with nothing to restore) hit this. Fixed properly —
`.gitattributes` (`*.sh text eol=lf`), not a server-side patch — committed,
pushed, re-archived, redeployed. Confirmed via `od -c` (byte-exact, not just
`cat -A`) that the fix holds before moving on.

A second gotcha on the same bug: after fixing the file, `docker compose ...
up -d --no-deps db-backup` alone did **not** clear the crash loop — the
running container's single-file bind mount was still pointing at the old
(pre-fix) inode. Needed `--force-recreate` to actually pick up the corrected
file.

### Bug #2, found by testing (not by reading code): missing `Secure` cookie flag

Nginx layers, DNS, certbot all worked on the first try. But testing an
actual login over the live HTTPS site showed no `secure` flag on the session
cookie — exactly the class of bug that morning's `ForwardedHeadersMiddleware`
fix was supposed to prevent. Isolated it methodically: tested the API
directly (`curl http://127.0.0.1:8081/...` with `X-Forwarded-Proto: https`
set by hand, bypassing both nginx hops entirely) — **still no Secure flag**,
proving the bug was in the API's own middleware config, not in nginx or the
two-hop topology.

Root cause: `KnownIPNetworks = { }` / `KnownProxies = { }` in the C# object
initializer. For an already-non-null collection property (which
`ForwardedHeadersOptions`'s always are, populated with framework defaults in
its constructor), `= { }` is collection-initializer syntax — it calls
`.Add()` once per listed element, and with zero elements that's a pure
no-op. **The framework's own loopback-only defaults were never actually
cleared.** In this deployment's real topology, `api` and `web` are two
separate containers talking over the Docker bridge network — `web`'s
connection to `api` arrives from a real bridge IP, never loopback — so the
untouched default silently rejected the forwarded headers on every request.
Fixed with explicit `.Clear()` calls on each collection instead of the
initializer shorthand. Verified: rebuilt, redeployed just `api`
(`--no-deps api`), re-tested the live login — `secure` now present.

## Why we built it this way

**Test the deployed system, not just the deployed code.** The morning's
`ForwardedHeadersMiddleware` addition passed a full local test (dummy
Production-mode run, login worked, tests green) — and was still wrong. The
gap was invisible locally because the local smoke test that morning was
single-hop (nginx directly to `api` via a published port), and the object-
initializer bug happens to be silent — it doesn't throw, it just quietly
keeps the wrong (but plausible-looking, non-empty) default. Nothing short of
actually running the exact deployed topology and checking a real response
header would have caught it. This is the second time in one day a bug in
this exact feature only surfaced by tracing the literal request path rather
than reading the code in isolation (the first: `X-Forwarded-Proto` being
overwritten instead of passed through in `web`'s own nginx, found a few
hours earlier by the same kind of tracing).

**Isolate before fixing.** When the cookie came back wrong, the instinct
could have been to start changing nginx configs (the layer physically
closest to where TLS terminates). Testing the API directly first — bypassing
both proxy hops with a hand-crafted header — proved in one command that the
bug lived in the API's own middleware setup, not in either nginx layer,
before touching anything. Saved a round of changing the wrong thing.

**Fix the file, not the deployed copy.** For the CRLF bug, the fast path
would have been `sed -i` directly on the server's extracted file. Did the
slower thing instead — fixed it in the repo, committed, and re-ran the exact
same archive→scp→extract pipeline — so the server's state stays exactly
reproducible from `git log`, matching the same principle svara-bg's own
history warns about (never assume the server matches the repo unless you
know for certain how it got that way).

## Files changed

- `.gitattributes` (new) — `*.sh text eol=lf`
- `src/MyDigitalLibrary.Api/Program.cs` — `ForwardedHeadersOptions`
  collections now `.Clear()`'d explicitly instead of `= { }`

## What's still open

- The informational-only Data Protection key-encryption note from the
  morning's history entry stands as-is (not a blocker, not touched today).
- No CI/CD pipeline for this app yet — every deploy so far has been the
  manual `git archive`/`scp`/`ssh` sequence in `deploy/README.md`. Worth
  building if deploys become frequent; not asked for yet.
