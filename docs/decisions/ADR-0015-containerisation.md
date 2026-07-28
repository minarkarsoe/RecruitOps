# ADR-0015 — Containerise everything; one image set for dev and delivery

- **Date:** 2026-07-27
- **Status:** Accepted (implemented; unverified — see below)
- **Implements:** the containerisation requirement recorded in
  [ADR-0004](ADR-0004-single-tenant-deployment.md)

## Context

Two pressures arrived at the same answer:

1. **Delivery.** ADR-0004 ships **one install per company**, some on customer hardware.
   It already listed containerised deployment and automated migrations as non-optional —
   without them, N installs are hand-built and the maintenance economics of
   [ADR-0011](ADR-0011-commercial-model-v2.md) collapse.
2. **Collaboration.** More than one person will now work on this. The stack is .NET 10 +
   Node + PostgreSQL + S3 storage; asking every contributor to install and version-match
   all four by hand produces "works on my machine" within a week.

There is also an immediate, concrete problem: **the backend has never been compiled**,
partly because a .NET 10 SDK was not available in the authoring environment.

## Decision

Containerise the whole stack, using **the same image definitions for local development
and for customer installs**. Local orchestration via `docker compose`.

- `backend/Dockerfile` — multi-stage: `sdk:10.0` restore/build → **`test` target** →
  publish → `aspnet:10.0` runtime, running as non-root (`$APP_UID`).
- `frontend/Dockerfile` — multi-stage Node 22 build → slim runtime.
- `docker-compose.yml` — `db` (Postgres 17, healthchecked), `storage`
  (MinIO, mirroring the on-prem path of [ADR-0013](ADR-0013-infrastructure-and-storage.md)),
  `api`, `web`.
- Configuration by environment variables only — no per-customer image variants
  ([ADR-0007](ADR-0007-productization-and-addons.md)).

### The `test` target earns its place

`docker build --target test ./backend` compiles **and runs the test suite** with no local
SDK. Given that nothing has ever been compiled, this is currently the **shortest path to a
first successful build** for anyone with Docker — and later it is what CI will call.

## Consequences

- A new contributor needs only Docker: `cp .env.example .env && docker compose up --build`.
- Dev and production differ in configuration, not in construction — the class of bug where
  something works locally and fails on a customer server largely disappears.
- `JWT_KEY` is a **required** compose variable with no default. Startup fails loudly rather
  than silently running on a weak key.
- Postgres and MinIO versions are pinned in one place, so every environment matches.

### Still missing (tracked, not solved here)

- ⚠️ **Automated EF migrations on startup** — required by ADR-0004 for unattended installs.
  **No migration exists yet**, so this cannot be wired up until the in-house model settles.
  Until then a fresh database has no schema.
- ⚠️ **No `package-lock.json`**, so the frontend image uses `npm install`, not `npm ci`.
  Builds are therefore not byte-reproducible. Commit a lockfile and switch.
- ⚠️ **Unverified.** No Docker daemon and no access to `mcr.microsoft.com` in the authoring
  environment, so these images have **never been built**. Expect to fix real errors on the
  first `docker compose up --build` — both container issues and the never-compiled C#.
- No production compose/orchestration yet (reverse proxy, TLS, subdomain routing per
  ADR-0004), and no `/api/version` endpoint.
- When [ADR-0012](ADR-0012-frontend-split.md) splits the frontend, `web` becomes **two**
  services (internal SPA + public SSR) behind the proxy.

## Alternatives considered

- **Documented local setup instead of containers** — rejected: doesn't solve delivery, and
  ADR-0004 requires containers regardless.
- **Dev containers only, hand-built production** — rejected: gives up the parity that is
  the main benefit, and leaves the N-install problem unsolved.
