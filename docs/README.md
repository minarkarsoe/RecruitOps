# RecruitOps — Knowledge Base

Single source of truth for this project. **Read this before starting any task.**
Every change to the product or codebase should leave a trace here.

> **Product:** In-house Recruitment Cloud System — a multi-tenant SaaS for a
> *company's own* talent acquisition department. It connects **In-house Recruiters**
> with **Department Hiring Managers**, covering requisition → sourcing → interview →
> offer → analytics.
>
> **Market:** domestic enterprises above ~1,000 staff (banks, FMCG/retail, telecom,
> factories, MFIs), sold partner-led as an **annual MMK subscription**
> ([ADR-0011](decisions/ADR-0011-commercial-model-v2.md)).
>
> **Delivery:** not a shared SaaS — **one instance + one database per company**,
> reached by subdomain, hosted on AWS or on the customer's own server
> ([ADR-0004](decisions/ADR-0004-single-tenant-deployment.md)).
>
> **Stack:** .NET 10 LTS modular monolith · PostgreSQL/RDS · Cloudflare R2 · two frontends
> (Vite SPA internal + Next.js SSR public).
>
> **MVP = Modules 1, 2, 3, 5** ([ADR-0006](decisions/ADR-0006-mvp-scope.md)).
> Sold as a **generic core product**; customization, deferred modules and integrations are
> **paid add-ons** ([ADR-0007](decisions/ADR-0007-productization-and-addons.md)) —
> which must never become per-customer code forks.
>
> ⚠️ **This project pivoted from an agency model on 2026-07-27.** See
> [ADR-0001](decisions/ADR-0001-pivot-to-inhouse.md). Anything describing external
> "clients", contract tiers, or a client-feedback portal is legacy and being removed.

## Map

| Area | Doc | What it answers |
|---|---|---|
| **What we're building** | [product/overview.md](product/overview.md) | Vision, users, value proposition |
| | [product/modules/](product/modules/) | The 7 core modules, feature by feature |
| **How it's built** | [architecture/overview.md](architecture/overview.md) | Stack, layers, conventions |
| | [architecture/data-model.md](architecture/data-model.md) | Target entity model |
| | [architecture/auth-and-tenancy.md](architecture/auth-and-tenancy.md) | JWT, RBAC roles, department scoping |
| | [architecture/deployment.md](architecture/deployment.md) | Per-company install, subdomain routing |
| **Getting started** | [architecture/local-development.md](architecture/local-development.md) | `docker compose up` — start here as a new contributor |
| **Decisions index** | [decisions/](decisions/) | 18 ADRs — read ADR-0011 before quoting any price, and ADR-0018 before assuming a role's reach |
| **Starting a session** | [status/NEXT-SESSION.md](status/NEXT-SESSION.md) | **Read this first** — where the product is, what's next, and the traps |
| **What's done** | [status/FEATURE-STATUS.md](status/FEATURE-STATUS.md) | ✅/🚧/⬜ per module — **current state** |
| | [status/CHANGELOG.md](status/CHANGELOG.md) | Track record: every change, dated |
| | [status/MIGRATION-PLAN.md](status/MIGRATION-PLAN.md) | Agency → in-house removal plan |
| **Why** | [decisions/](decisions/) | ADRs — decisions and their rationale |
| **Source material** | [reference/](reference/) | Original product docs (Burmese) |

## Maintenance rules

These keep the knowledge base honest. Follow them on **every** change:

1. **Ship code → update [FEATURE-STATUS.md](status/FEATURE-STATUS.md)** in the same change. Status marks: ✅ done · 🚧 partial · ⬜ not started · ❌ removed.
2. **Any user-visible or structural change → add a [CHANGELOG.md](status/CHANGELOG.md) entry** (date, what changed, why, affected files).
3. **A decision that's hard to reverse → write an ADR** in `decisions/` (schema, auth, external dependency, product scope). Number sequentially; never rewrite history — supersede instead.
4. **Spec changes → update the module doc first**, then the code. The module doc is the contract.
5. **Never delete a doc.** Mark it superseded and link forward.
