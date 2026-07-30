# Architecture Overview

Stack and conventions are governed by `/CLAUDE.md` (the project constitution).
This doc explains the shape; CLAUDE.md states the rules.

## Stack

- **Backend:** **.NET 10 (LTS)** / ASP.NET Core Web API — a **modular monolith** built with
  Clean Architecture layering, so modules can be split out later if needed
  ([ADR-0010](../decisions/ADR-0010-dotnet-10-lts.md))
- **Database:** **PostgreSQL on AWS RDS** (Npgsql), JSONB for customer-defined fields
  ([ADR-0013](../decisions/ADR-0013-infrastructure-and-storage.md))
- **Object storage:** **Cloudflare R2** (zero egress) behind an S3-compatible abstraction,
  so on-premise installs can use MinIO
- **Frontend:** **two apps** ([ADR-0012](../decisions/ADR-0012-frontend-split.md)) —
  Vite + React SPA for internal dashboards, Next.js SSR for public job pages (Open Graph
  previews for social sharing) — sharing a design-system package
- **Auth:** self-issued JWT bearer — see [auth-and-tenancy.md](auth-and-tenancy.md)
- **Delivery:** one instance + one database per company — see [deployment.md](deployment.md)

## Layers (`backend/src`)

```
Api            controllers, DI wiring, auth config, Swagger, CORS  ── composition root
  → Application    use-case interfaces, DTOs, ICurrentTenant
       → Domain    entities, enums, pure domain services; no outward dependencies
Infrastructure  EF Core AppDbContext, service implementations  → Application + Domain
```

Rules: Domain depends on nothing. Application defines interfaces. Infrastructure
implements them. Api wires everything. Async all the way.

## Repository layout

Layout ([ADR-0012](../decisions/ADR-0012-frontend-split.md)):

```
backend/
  src/{Domain,Application,Infrastructure,Api}/
  tests/{RecruitOps.Domain.Tests,RecruitOps.Api.Tests}/
frontend/
  internal/     Vite + React SPA — authenticated dashboards
  public/       Next.js SSR — public job pages, application forms, OG metadata
packages/
  ui/           shared design-system components + Tailwind preset
  types/        shared API types (mirror backend DTOs)
docs/           ← this knowledge base
.claude/        agent config: subagents + slash commands
```

## Cross-cutting patterns

| Pattern | Where | Notes |
|---|---|---|
| **Tenant isolation** | `ITenantScoped` + global query filters in `AppDbContext` | Every tenant-owned entity must implement it. Non-negotiable. |
| **Approval chains** | Modules 1 & 6 | Build **once**, reuse for requisitions and budget plans. |
| **Stage history** | Module 2, consumed by Module 5 | Append-only. Must be written from day one or analytics can't be back-filled. |
| **Status vocabulary** | Domain enums ↔ `frontend/lib/types.ts` | Fixed vocabulary; the two sides must stay in sync. |
| **Secrets** | Config/user-secrets/env only | Never committed. Enforced by review + `.gitignore`. |

## Testing

- `RecruitOps.Domain.Tests` — pure domain logic (fast, no I/O)
- `RecruitOps.Api.Tests` — `WebApplicationFactory` integration tests over the real
  pipeline (auth, tenant filters, endpoints) with an in-memory DB
- `frontend/tests` — Vitest over pure helpers/components

## Known environment issues

- **The authoring sandbox still cannot build .NET** — no SDK, and `mcr.microsoft.com` /
  `nuget.org` are blocked by the network allowlist. **CI is the build environment**: every push
  runs `docker build --target test ./backend`, currently green at 169/169. Push early rather
  than writing C# you cannot compile.
- Git operations partly fail on the mounted Windows folder (lock files can't be
  removed by the sandbox). Use a native Windows terminal for git.
