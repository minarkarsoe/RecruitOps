# Migration Plan — Agency → In-house

**Decision:** remove agency code outright (not deprecate). See [ADR-0001](../decisions/ADR-0001-pivot-to-inhouse.md).
**Status:** 🚧 Steps 1–4 **done** (2026-07-27); Steps 5–6 pending.
⚠️ Code is written but **still never compiled** — no .NET SDK was available. Run
`dotnet build` + `dotnet test backend/tests` locally to verify.

Do this on a feature branch (`refactor/pivot-to-inhouse`), never on `main`.

---

## ✅ Step 1 — Delete agency-only files *(done)*

```
backend/src/Domain/Entities/Client.cs
backend/src/Domain/Entities/Contract.cs
backend/src/Domain/Enums/ClientTier.cs
backend/src/Domain/Enums/ContractStatus.cs
backend/src/Domain/Enums/ClientFeedback.cs
backend/src/Domain/Services/ContractStatusCalculator.cs
backend/src/Application/DTOs/ClientListItemDto.cs
backend/src/Application/Interfaces/IClientService.cs
backend/src/Application/Interfaces/IContractService.cs
backend/src/Infrastructure/Services/ClientService.cs
backend/src/Api/Controllers/ClientsController.cs
backend/src/Api/Controllers/ContractsController.cs
backend/tests/RecruitOps.Domain.Tests/ContractStatusCalculatorTests.cs
frontend/app/clients/page.tsx
frontend/components/ui/TierBadge.tsx
frontend/lib/contract.ts
frontend/tests/contract.test.ts
```

## ✅ Step 2 — Fix the resulting breakage *(done)*

| File | Change |
|---|---|
| `Infrastructure/Persistence/AppDbContext.cs` | Drop `Clients`/`Contracts` DbSets + their query filters |
| `Infrastructure/DependencyInjection.cs` | Drop `IClientService` registration |
| `frontend/lib/types.ts` | Remove `ClientTier`, `ContractStatus`, `ClientListItem`, `ClientFeedback` |
| `frontend/components/ui/StatusPill.tsx` | Remove contract statuses; apply new pipeline vocabulary |
| `frontend/app/layout.tsx` | Remove the Clients nav link |
| `backend/tests/RecruitOps.Api.Tests/*` | Isolation tests use `Client` — **rewrite against `Department`**, don't delete (they're the isolation proof) |

## ✅ Step 3 — Rename to in-house concepts *(done)*

| From | To | Notes |
|---|---|---|
| `Tenant` | `Company` | Entity + `DbSet` + references |
| `Job` | `JobPosting` | Now created from an approved requisition |
| Policy `AgencyStaff` | `RecruitmentStaff` | `Api/Auth/Policies.cs` + all `[Authorize]` attributes |
| `UserRole` values | `Admin`, `HrDirector`, `Recruiter`, `HiringManager`, `Approver` | Drop `Client`; collapse Senior/Junior recruiter |
| `PipelineStatus` values | Drop `SentToClient`; `Placed` → `Hired`; add `Applied`, `Screening`, `Offer` | Backend enum **and** `frontend/lib/types.ts` together |

## 🚧 Step 4 — Introduce the in-house core *(partial: Department + UserDepartment done)*

Minimum to make Module 1 possible: ~~`Department`~~ ✅, `Requisition`,
`RequisitionApproval`, `ApprovalChain` / `ApprovalChainStep`, `JdTemplate`.

Also added: `UserDepartment` (many-to-many access per ADR-0003), `Company` (renamed from
`Tenant`), `JobPosting` (renamed from `Job`, now department-owned).

## ⬜ Step 5 — Update the design system

Revise `/RecruitOps_Design_System.md` per the impact table in
[data-model.md](../architecture/data-model.md#design-system-impact): remove tier badge,
client feedback bar, and the client-review portal card; repurpose the expiry card;
update the status-pill vocabulary; add a public job page + application form design.

## ⬜ Step 6 — Docs & verification

- Update [FEATURE-STATUS.md](FEATURE-STATUS.md) and [CHANGELOG.md](CHANGELOG.md)
- `dotnet build` + `dotnet test backend/tests` + `npm run build --prefix frontend` — **must pass**
- Run the `code-reviewer` and `security-reviewer` subagents on the diff

---

## Sequencing note

Because **no EF migration has ever been created**, there is no production schema to
migrate — the entities can be deleted and reshaped freely with no data-migration cost.
**This is the cheapest moment to do this pivot.** Create the first migration only after
the in-house model has settled.

## Open decision before starting

`HiringManager` department-scoping (see [auth-and-tenancy.md](../architecture/auth-and-tenancy.md))
must be decided before Module 1 — it's a second filter dimension beyond tenant and
affects every query. Write an ADR for it.
