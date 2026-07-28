# ADR-0006 — MVP scope: Modules 1, 2, 3, 5

- **Date:** 2026-07-27
- **Status:** Accepted
- **Related:** [ADR-0004](ADR-0004-single-tenant-deployment.md)

## Decision

The MVP is **Module 1 (Requisition & Approval)**, **Module 2 (ATS & Sourcing)**,
**Module 3 (Interview & Assessment)** and **Module 5 (Reporting & Analytics)**.

Deferred: **Module 4** (Offer & Pre-boarding), **Module 6** (Planning & Budgeting),
**Module 7** integrations (RBAC from Module 7 is already built and stays).

**Immediately after the MVP:** **Module 8 — Multi-Channel Sourcing** (Viber/Telegram/
Facebook bots), the product's stated primary differentiator. It is sequenced *after*
because bots are an intake channel into Module 2's pipeline — building the channel before
the thing it feeds inverts the dependency. See
[ADR-0014](ADR-0014-multi-channel-sourcing.md).

## Why this is a coherent MVP

1, 2 and 3 form the **complete internal hiring loop** — raise a vacancy, get it
approved, source and track candidates, interview and score them. That loop *is* the
product's stated core problem: recruiter↔hiring-manager collaboration.

Module 5 then makes that loop **sellable to the buyer**. The person signing off is an
HR Director or Management, and their stated benefit is data-driven decisions. Without
reporting, the system is a tool for staff; with it, it's an argument for the purchase.

## Build order (dependencies are real)

```
1. Module 1 — Requisition & Approval
      ↓ produces approved requisitions → job postings
2. Module 2 — ATS & Sourcing        ⚠️ write ApplicationStageHistory from day one
      ↓ produces applications in a pipeline
3. Module 3 — Interview & Assessment
      ↓ produces interviews + scorecards
4. Module 5 — Reporting & Analytics  (consumes all of the above)
```

**Module 5 must be built last** and **depends on data written earlier**:

- *Time-to-Hire / Time-to-Fill*, *Pipeline Conversion Rates* → require
  `ApplicationStageHistory` (append-only stage transitions).
- *Source of Hire* → requires the source channel recorded on every candidate/application.

⚠️ **If Module 2 ships without stage history, Module 5 can never be back-filled.**
That table is the single most important thing to get right early.

## Consequences of deferring Module 4

With no Offer module, the pipeline ends at the `Offer` / `Hired` stage set **manually**
by a recruiter; the offer letter is produced outside the system. Acceptable for an MVP,
with two caveats:

- *Time-to-Hire* will be measured to the manually-set `Hired` stage, so its accuracy
  depends on recruiters updating the stage promptly. Document this definition.
- The "notify IT/Admin on acceptance" automation is lost — customers will do it by email.

## Prerequisite work before Module 1

From [MIGRATION-PLAN.md](../status/MIGRATION-PLAN.md), and not optional:

1. Remove the agency code (Client / Contract / tiers / client feedback).
2. Rename to in-house concepts (`Tenant`→`Company`, roles, pipeline vocabulary).
3. Add `Department` + `UserDepartment`, and implement department scoping
   ([ADR-0003](ADR-0003-department-scoping.md)) — Module 1 is meaningless without it.
4. **Compile and test the backend for the first time** — none of it has ever been run.
5. Create the first EF migration once the in-house model has settled.

## ✅ Module 2 is unblocked

The OCR / Smart Match question is resolved by
[ADR-0008](ADR-0008-document-extraction-and-ai-profiling.md): local text extraction in the
MVP (works with no internet), with AI structuring as an optional, API-key-gated
enhancement. Nothing now blocks the MVP build order.
