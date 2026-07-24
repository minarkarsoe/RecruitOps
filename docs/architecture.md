# RecruitOps — Architecture

B2B Recruitment-Agency-as-a-Service (RAaaS). This document maps the product's
five functional modules onto the codebase. Stack and layout follow `CLAUDE.md`.

## Stack

- **Backend:** .NET 8 / ASP.NET Core, Clean Architecture, EF Core + **PostgreSQL** (Npgsql).
- **Frontend:** Next.js (App Router) + React + TypeScript, Tailwind theme from the
  "Clear Pipeline" design system (`RecruitOps_Design_System.md`).

## Backend layers (`backend/src`)

```
Api            controllers, DI wiring, Swagger, CORS  ── composition root
  -> Application    use-case interfaces (IClientService, IPortalService, ...), ICurrentTenant
       -> Domain    entities + fixed-vocabulary enums; no outward dependencies
Infrastructure  EF Core AppDbContext, DependencyInjection  -> Application + Domain
```

Rule (per CLAUDE.md): Domain depends on nothing; Application defines interfaces;
Infrastructure implements them; Api wires everything. Async all the way.

## Module → code map

| # | Module | Where it lives (scaffolded) |
|---|--------|------------------------------|
| 1 | Multi-tenant + RBAC | `Domain/Common/ITenantScoped`, `Tenant`/`User`, `UserRole`; global query filters in `AppDbContext`; `ICurrentTenant` resolver (stubbed in `Program.cs`). |
| 2 | Client Portal & CRM | `Client` + `ClientTier`, `Contract` + `ContractStatus` (expiry alerts), `PortalLink`, `ClientFeedback`; `PortalController`/`ContractsController`; frontend `app/portal/[token]`. |
| 3 | Omni-channel job posting | `Job` + `JobStatus`, `JobChannelPost` + `SourceChannel` (posting + inbound tracking); `JobsController`. |
| 4 | Smart deduplication | `Candidate` (Email/Phone + `MergedIntoId`), `Application` history; index config TODO in `AppDbContext`. |
| 5 | Excel data migration | Not yet scaffolded — planned as an Application import service + Api upload endpoint. |

## Data model (planned)

```
Tenant 1─* User
Tenant 1─* Client 1─* Contract
Client 1─* Job 1─* JobChannelPost
Job 1─* Application *─1 Candidate
Job 1─* PortalLink   (shortlist shared with client, no login)
```

## Design system

The Tailwind theme (`frontend/tailwind.config.ts`) encodes the exact color tokens,
font stacks (Inter / Bricolage Grotesque / IBM Plex Mono / Noto Sans Myanmar), radii,
and shadows. Status vocabulary is fixed in both `Domain/Enums` and `lib/types.ts` and
must stay in sync. `StatusPill` is the signature component.

## Open decisions (from the spec's discussion points + CLAUDE.md gaps)

- **Auth**: JWT vs. ASP.NET Identity vs. external IdP — `CLAUDE.md` leaves this TBD.
- **Tenant resolution**: subdomain vs. header vs. claim — currently a stub.
- Portal link token strategy (unguessable, expiring, revocable).
- Channel posting integrations (Facebook / LinkedIn / Telegram APIs).
- Pricing tiers (Starter / Growth / Pro) + usage credits.
- Redis cache — listed in CLAUDE.md as optional; not scaffolded.
