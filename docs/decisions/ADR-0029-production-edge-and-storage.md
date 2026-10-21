# ADR-0029 — The production stack: TLS at the bundled proxy, a private CV bucket, MinIO from Chainguard

- **Date:** 2026-10-16
- **Status:** ✅ Accepted — built and run end to end the same day
- **Scopes:** `docker-compose.prod.yml`, `docker-compose.yml`, `infra/nginx/nginx.conf`
- **Constrained by:** [ADR-0004](ADR-0004-single-tenant-deployment.md) (one instance per company,
  on our servers or the customer's), [ADR-0013](ADR-0013-infrastructure-and-storage.md) (MinIO on
  premise, R2 hosted, one S3 abstraction), [ADR-0015](ADR-0015-containerisation.md) (one image set
  for dev and delivery), [ADR-0016](ADR-0016-login-brute-force-protection.md) (the API trusts the
  last `X-Forwarded-For` entry only)

## Context

`docker-compose.prod.yml` had never been run. Running it for the first time, on 2026-10-16, found
that it could not have served a customer safely, or at all:

1. **It did not start.** `minio/minio` and `minio/mc` no longer exist on Docker Hub — the registry
   API returns 404 for both and `docker pull` reports "repository does not exist". Upstream stopped
   publishing community images in October 2025 and archived the repositories in 2026. Every
   `docker compose up`, dev or prod, failed at storage.
2. **Every CV was public.** The bucket job ran `mc anonymous set download`. Measured against a
   bucket set up that way, an anonymous request could **list every object (200) and download any
   CV (200)** — no credentials. Prod does not publish port 9000, so the reach was anything on the
   Docker network; dev publishes 9000 on all interfaces, so it was anyone on the developer's LAN.
   Nothing used the access: CVs reach a browser only through `GET /api/applications/{id}/resume`,
   which checks who is asking, and no code hands a storage URL to a client.
3. **There was no TLS.** Compose published 443; nginx listened only on 80. Sign-in, passwords,
   tokens and CVs travelled in plain text unless an operator built something in front. The VPS
   guide's `certbot --nginx` would have installed a second nginx on the host, competing with the
   container for port 80.
4. **The public job page was broken.** Next.js serves its scripts and stylesheets from `/_next/`;
   the proxy sent only `/jobs/` to the public app, so `/_next/` fell through to the internal SPA,
   which answered each with its own `index.html`. The page rendered unstyled and the application
   form never hydrated.
5. **Nobody could sign in to a fresh install.** The API creates the first Administrator from
   `Seed:AdminEmail` / `Seed:AdminPassword`, but the prod compose file never passed them.

## Decision

1. **MinIO comes from `chainguard/minio`, pinned by digest.** Chainguard rebuilds it from
   MinIO's AGPL source and signs it (the product owner's choice over the `pgsty` fork and
   building it ourselves). The free tier publishes only `latest`, so the compose files pin
   `latest@sha256:…`; three-month-old digests were checked and still resolve. The image carries
   `mc` and a shell, so **one image** serves the server, its healthcheck (`mc ready local`) and
   the bucket job — no separate client image. Bumping it is deliberate: the command is in a
   comment above the pin, in both compose files.
2. **The image runs as uid 65532, so a one-shot `storage-init` hands it the volume**
   (`chown -R 65532:65532 /data`, as root) before MinIO starts. Volumes written by `minio/minio`
   belong to root, and on them the new image exits with "Unable to write to the backend" —
   without this, an upgrade loses storage. It runs on every `up`; it is idempotent.
3. **The CV bucket is private, enforced on every start.** The bucket job runs
   `mc anonymous set none` and then reads the policy back; if it is not `private` the job exits 1,
   and the API — which now depends on it completing — does not start. An install whose bucket an
   older compose file made public is closed by its next `up`. Failing closed was preferred over
   running with a policy nobody checked.
4. **TLS terminates at the bundled nginx.** Port 443 serves the product (TLS 1.2/1.3, Mozilla
   intermediate ciphers, HTTP/2, `Strict-Transport-Security: max-age=31536000` without
   `includeSubDomains`). Port 80 serves only `/.well-known/acme-challenge/` and redirects
   everything else. The certificate is **two files in a directory**, `TLS_CERT_DIR`
   (`fullchain.pem`, `privkey.pem`), which compose **requires** — `up` refuses to start without it
   rather than fall back to plain HTTP. Any CA fits that contract: Let's Encrypt (issued with
   `--standalone` before the first `up`, renewed with `--webroot` into `ACME_WEBROOT`), the
   customer's own, or a Cloudflare Origin certificate behind "Full (strict)".
5. **`/_next/` goes to the public app**, and the prod compose file passes `SEED_ADMIN_EMAIL` /
   `SEED_ADMIN_PASSWORD` through — the API's existing rules apply (used only while there are no
   users, 12+ character password outside Development).

## Verified (2026-10-16, real containers)

Built from this commit's API and frontends, behind this commit's compose file and nginx config:
an upgrade from a simulated old install (root-owned volume, public bucket, one CV) came up, the
old CV was intact, and anonymous list/download of old and new objects returned **403**; a fresh
install came up and reported healthy; a CV uploaded through the API downloaded byte-identical
through it, and 401 without a token; the whole hiring flow (department → requisition → approval →
posting → public apply) ran over HTTPS; a browser loaded the job page styled and hydrated, was
redirected from `http://` to `https://`, and signed in; a spoofed `X-Forwarded-For` was ignored
and the audit log recorded the real peer; TLS 1.1 and non-forward-secret ciphers were refused; a
forced bucket-job failure left the API in `Created`.

The images were built on the host (the sandbox's TLS-intercepting proxy defeats NuGet and npm
inside `docker build`), so the Dockerfiles themselves were not re-exercised — CI builds the
backend one on every push.

## Consequences

- **Upgrading an existing install:** put a certificate in `TLS_CERT_DIR` first — `up` will refuse
  without one. The first `up` re-owns the storage volume and closes the bucket; both are logged
  (`docker compose logs storage-init create-buckets`).
- **HSTS is sticky.** Once a browser has seen it, that host is HTTPS-only for a year. Deploying
  with a certificate browsers do not trust makes the warning unbypassable — use a real one.
- **Behind Cloudflare,** a Tunnel must now point at `https://proxy:443` (port 80 only redirects);
  the guide is updated.
- `nginx.conf` is bind-mounted as a single file: after editing it, `docker compose restart proxy`.
  `nginx -s reload` keeps reading the old file once an editor has replaced it (measured). The
  certificate directory is a directory mount, so `nginx -s reload` does pick up renewed files.
- **Chainguard's free tier is `latest` only** — no version tags, and nothing promises old digests
  are kept forever. If a pinned digest ever stops resolving, bump it; if Chainguard's terms
  change, building from source is the fallback, and the compose files change in one line each.
- Not changed, still open: MinIO runs with its root credentials, which the API also uses (a
  least-privilege service account is the next step); storage backups are not in the runbook,
  only Postgres.

## Security review (2026-10-16) — no Critical or High; what changed

- **M1 — the documented renewal would have failed.** The inline deploy hook used `{a,b}` brace
  expansion, which certbot's `/bin/sh` (dash) leaves literal, and the first issuance used
  `--standalone`, which renewals repeat — needing port 80 the proxy holds. With HSTS, an expired
  certificate is an outage nobody can click past. Now: `infra/certbot/deploy-hook.sh` (bash,
  atomic copy, key `600`, `nginx -t` before reload), and issuance through the running proxy with
  `--webroot` after a throwaway placeholder. **Rehearsed against Pebble** (Let's Encrypt's test
  CA): issue, then a renewal from the saved config alone (served serial changed), then `--dry-run` (the dry run pointed at Pebble with `--server`: `--dry-run` always switches to Let's Encrypt's staging CA, which the sandbox could not reach — in production that is what it should use).
- **M2 — published secrets that passed validation.** The VPS guide's example passwords and JWT
  key were long enough to be accepted. The guide now leaves every secret blank (a blank secret
  stops `up`), and the API refuses **every signing key the repository has printed** — the VPS
  guide's and `backend/src/Api/.env.example`'s were both accepted until now.
- **L1 — closing the bucket was not complete.** Measured: `mc anonymous set none` on the bucket
  leaves a rule scoped to a prefix in place (the policy reads `custom`, files stay readable), and
  a custom JSON policy granting `GetObject` *lists* as `=> none`. The job now removes every listed
  rule too, then requires all three: policy `private`, no rules listed, and an anonymous listing
  refused. Tested against five policy shapes, and with the closing steps disabled (it refuses).
- **L3** prod refuses `minioadmin`; **L8** `storage-init` runs `chown -hR` with no network,
  `no-new-privileges`, and only `CHOWN` + `DAC_READ_SEARCH` (measured: without the second, every
  `up` after the first fails); **L9** only `/_next/static/` reaches Next; **L10/L11** runbook:
  remove the seed and recreate, `install -m 600` for keys; **L12** dev MinIO on loopback.
- **Re-review (same day): no new Critical/High; four Lows, all fixed.** N1 — `DAC_READ_SEARCH`
  re-enables `open_by_handle_at` (the "Shocker" breakout syscall); `storage-init` now uses
  `DAC_OVERRIDE`, measured to work on repeated runs. N2 — the hook's `.env` parsing broke on the
  runbook's own example (inline comment); it now asks Docker which directory is mounted into the
  proxy. N3 — a dry run skips deploy hooks; the runbook adds a manual hook run, the hook restores
  the previous pair if `nginx -t` rejects the new one (tested with a mismatched key), and the
  clone must be root-owned. N4 — seven hand-written policy shapes (mid-path wildcards,
  conditions, `s3:Get*`, a conditioned `ListBucket`, …) made the job stop but left the bucket
  readable; it now replaces the document with an empty one and resets — all twelve shapes tested
  end closed.
- **Left open, deliberately:** **L4** — the proxy headers stay at server level (which protects
  every *new* location) with a warning against per-location headers, rather than an `include` in
  each location (which would protect against the other mistake); **L5** — redirects and the
  catch-all follow the `Host` header (needs a configured hostname; low risk in a one-company
  install); **L6** — behind Cloudflare the API sees Cloudflare's address: ✅ **built 2026-10-21**
  (`EDGE_PROXY` = none | cloudflare | cloudflare-tunnel selects an nginx `realip` include; the
  tunnel is a bundled compose profile that shares the proxy's network namespace, so nginx trusts
  exactly 127.0.0.1; measured on the stack — see the Cloudflare guide); **L2** — the policy is enforced on `up`, not on
  `restart`; **L7** — no signature check or scanning of the pinned image yet.

## Follow-up, 2026-10-21 — the API no longer holds MinIO's root credentials

Listed as an open item since this ADR: the API used `MINIO_USER` / `MINIO_PASSWORD`, so a leaked API
configuration was a storage admin key — every bucket, every user, the server's settings.
`create-buckets` now also creates the API's own account (`MINIO_APP_USER` / `MINIO_APP_PASSWORD`,
required) with a policy of `s3:GetObject`, `PutObject`, `DeleteObject` on `recruitops-cvs/*` and
`s3:ListBucket`, `GetBucketLocation` on the bucket (without `ListBucket` a missing CV answers 403,
not 404). Every start re-applies it and proves it: a probe object is written, read and deleted;
`mc admin info`, creating a bucket and seeing any other bucket must all fail. The API's
`AutoCreateBucket` is off in production. Measured 2026-10-21 on the stack: two consecutive runs
succeed, a CV uploaded through the API comes back byte-for-byte, and an app user equal to the root
user, or a short password, stops the job. The image has no `grep`; the scripts use shell built-ins
only (a `grep` check had passed by failing to run).

