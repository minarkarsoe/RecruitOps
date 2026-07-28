# ADR-0001 — Pivot from Recruitment Agency SaaS to In-house Recruitment Cloud

- **Date:** 2026-07-27
- **Status:** Accepted
- **Supersedes:** the original agency product spec (`docs/reference/B2B Recruitment Agency Platform.docx`)

## Context

The project began as **RAaaS** — a B2B platform sold to *recruitment agencies*, whose
customers were external client companies. That model shaped the domain: `Client` (an
external hiring company), `Contract`/SLA with Gold/Silver/Bronze tiers, and a no-login
**client portal** where agency clients reviewed shortlisted CVs and gave one-click
feedback.

A new product overview (`docs/reference/In-house Recruitment - Product Overview.pdf`)
redefines the product as an **In-house Recruitment Cloud System**: sold to a *company*,
used by that company's own talent acquisition department. Its stated central problem is
**internal collaboration between In-house Recruiters and Department Hiring Managers** —
explicitly "not for external recruitment agencies".

## Decision

Pivot to the in-house model. Adopt the 7 modules in the new overview as the product
scope. Remove agency-specific concepts rather than deprecating them, so the codebase
matches the product with no ambiguity.

## Consequences

### Preserved (the pivot was cheap because these are model-agnostic)

- **Multi-tenancy** — tenant simply means *company* instead of *agency*. Isolation
  mechanism unchanged.
- **JWT auth + RBAC plumbing** — role *names* change, mechanism doesn't.
- **Clean Architecture layering, test setup, design tokens.**
- **Candidate / Application / pipeline / duplicate detection** — the ATS core exists in
  both models.

### Removed

`Client`, `Contract`, `ClientTier`, `ContractStatus`, `ClientFeedback`,
`ContractStatusCalculator`, plus the Client CRM feature slice (service, controller,
frontend table, `TierBadge`). See [MIGRATION-PLAN.md](../status/MIGRATION-PLAN.md).

### Changed meaning (watch for confusion)

- **Shareable link**: was a client CV-review portal → now a **public job page for applicants**.
- **Approval**: was client feedback on candidates → now **internal requisition/budget approval**.
- **Contract expiry tracking** → replaced by **budget & headcount planning** (Module 6).
- **Pipeline vocabulary**: `SentToClient` removed, `Placed` → `Hired`.

### New scope, not previously planned

Requisition & approval workflows, JD templates, OCR resume parsing, AI Smart Match,
interview scheduling + scorecards, offer generation + e-signature, analytics,
budgeting, HRMS/calendar integrations. This is **substantially larger** than the
agency scope — sequencing matters more than it did.

## Alternatives considered

- **Keep both models** (agency + in-house in one product): rejected — doubles the
  domain model and the permission surface for no confirmed demand.
- **Deprecate rather than delete** the agency code: rejected — dead code in a young
  codebase misleads future work, and git history preserves it anyway.
