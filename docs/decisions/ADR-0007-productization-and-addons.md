# ADR-0007 — Generic core product, customization sold as add-ons

- **Date:** 2026-07-27
- **Status:** Accepted
- **Related:** [ADR-0004](ADR-0004-single-tenant-deployment.md), [ADR-0005](ADR-0005-commercial-model.md)

## Decision

Sell **one generic product** with a defined standard feature set. Anything a customer
wants beyond it is a **paid add-on**, quoted separately.

## The rule that makes this survivable

> **A customization must never become a per-customer branch of the codebase.**

This is not a style preference. Per [ADR-0004](ADR-0004-single-tenant-deployment.md) we
already run **N separate installs**; if each also runs *different code*, then every bug
fix must be ported N times, every upgrade is bespoke, and the 10–20% maintenance fee
([ADR-0005](ADR-0005-commercial-model.md)) stops covering costs almost immediately.
Forking per customer is the failure mode that kills this business model.

Instead, every customization must land in one of four buckets:

| # | Bucket | Mechanism | Marginal cost per customer |
|---|---|---|---|
| 1 | **Configuration** | Data in their own DB — custom fields, approval chains, scorecard criteria, branding, roles | ~zero |
| 2 | **Feature flag** | Module/feature toggled per install via env config | ~zero |
| 3 | **Extension point** | Defined hook — templates, export formats, webhooks, import mappers | Low |
| 4 | **Core change** | New capability in shared code, shipped to everyone (flag-gated if needed) | One-time; benefits all |

Anything that fits none of these should be **repriced or refused**, not forked.

## The spec is already mostly configuration — use that

Much of what customers will ask for is, by design, already data rather than code:

| Spec feature | Module | Bucket |
|---|---|---|
| Customizable application form (custom fields) | 2.2 | Configuration |
| Dynamic approval workflow per org structure | 1.3 | Configuration |
| JD template library | 1.2 | Configuration |
| Standardized scorecards (criteria) | 3.3 | Configuration |
| Custom report builder | 5.4 | Configuration |
| RBAC roles/permissions | 7.1 | Configuration |
| HRMS / payroll integration | 7.2 | Extension point |
| Offer letter templates | 4.1 | Configuration |

**Implication for build order:** these must be built as *configurable from day one*, not
hard-coded with "we'll make it configurable later". Retrofitting configurability after
three customers have gone live is exactly how forks get created.

## What is fair to charge as an add-on

- A **deferred module** (4, 6, or 7 integrations) for a customer who wants it early
- A **specific external integration** — their particular HRMS, payroll or job board
- **Data migration** from their existing system (spreadsheets, legacy ATS)
- **Bespoke reports** or export formats beyond the report builder
- **Custom branding** beyond the standard theming
- Extra **training / onboarding** beyond the standard package

## Consequences

- Requires a **feature-flag mechanism** early — cheap now, invasive later.
- Requires **discipline in sales**: a promise made in a deal that fits none of the four
  buckets becomes a permanent engineering tax. Quote add-ons against the buckets above.
- The **standard feature set must be written down and published** — otherwise "generic"
  is undefined and every deal renegotiates scope. The MVP
  ([ADR-0006](ADR-0006-mvp-scope.md)) is the first version of that list.
- Add-on revenue is a **direct mitigation** for the year-2 revenue cliff identified in
  ADR-0005 — it is the recurring-revenue leg of a perpetual-licence model.

## Alternatives considered

- **Bespoke build per customer** — rejected: highest revenue per deal, but doesn't scale
  and destroys the maintenance economics.
- **Everything configurable, no add-ons** — rejected: over-engineers the product for
  needs no customer has yet paid for.
