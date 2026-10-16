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
  only Postgres; the dev compose file still publishes 9000/9001 on all interfaces with the
  well-known `minioadmin` default.
