# ADR-0003 — Hiring Managers are scoped to their own department

- **Date:** 2026-07-27
- **Status:** Accepted
- **Related:** [ADR-0001](ADR-0001-pivot-to-inhouse.md), [ADR-0004](ADR-0004-single-tenant-deployment.md)

## Context

In the in-house model a Hiring Manager is a **department manager**, not a recruiter.
The Sales manager raising a Sales vacancy has no business reading candidates for the
Finance vacancy — those applications contain salary expectations, contact details and
interview feedback about identifiable people.

Recruiters and HR, by contrast, work across all departments by definition.

This is a **second filter dimension** beyond tenant, and unlike tenant it cannot be
solved by physical separation (see ADR-0004) — one company's database contains all its
departments.

## Decision

`HiringManager` sees only data belonging to **their own department**.

Scoping applies to: `Requisition`, `JobPosting`, `Application`, `Candidate` (via
application), `Interview`, `Scorecard`, and any report row derived from them.

Roles **not** department-scoped: `Admin`, `HrDirector`, `Recruiter`, `Approver`
(an approver must see what they're approving, which may cross departments).

> ⚠️ **Amended by [ADR-0018](ADR-0018-approver-candidate-data-exclusion.md).** The line above
> is an argument about **requisitions**, and it still holds for them. It was read by every
> service as a general "sees everything", which handed `Approver` every candidate, pipeline
> board, scorecard and note in the company. `Approver` is now excluded from candidate data on
> a separate axis; it remains un-scoped here. Ask `RoleScope`, never a role literal.

### Mechanism — explicit authorization, not a global query filter

Unlike tenant isolation, department scoping is **not** implemented as an EF global
query filter. Instead the application layer applies an explicit department predicate,
resolved from a `DepartmentAccess` abstraction similar to `ICurrentTenant`.

**Why not a global filter:**

1. It's **conditional** — it must apply for `HiringManager` and not for four other
   roles. Global filters are static per entity; encoding "sometimes" into them means
   `IgnoreQueryFilters()` sprinkled through the code, which is exactly the pattern that
   makes filters unsafe (we already hit this with login and seeding).
2. `Candidate` is only *indirectly* departmental — through its applications. A person
   who applied to both Sales and Finance is visible to both managers. A static filter
   can't express that; a query predicate can.
3. Reports (Module 5) need aggregate queries where the scoping rule differs from row
   access. Explicit is clearer.

**Consequence:** because it isn't automatic, it can be *forgotten*. Mitigations:
- Every list/detail use case that returns scoped entities must have a test proving a
  Hiring Manager from department A cannot read department B's row (403/404, not empty list).
- The `security-reviewer` subagent must check department scoping on any new endpoint
  returning the entities listed above.

## Edge cases to handle

- **A manager managing two departments** — model access as a set (`UserDepartment`
  many-to-many), not a single `DepartmentId` on `User`. Cheap now, painful later.
- **Interview panel across departments** — a manager invited as an interviewer for
  another department's candidate must see *that candidate*, scoped to that interview.
  Grant via interview participation, not department.
- **Transfers/reorgs** — access follows current department membership; historical rows
  keep their original department. A manager who moves loses access to the old
  department's data.
- **Not-found vs forbidden** — return 404 rather than 403 for out-of-scope rows, so
  existence isn't leaked.

## Alternatives considered

- **Global query filter on `DepartmentId`** — rejected, see above.
- **No department scoping (all internal staff see everything)** — rejected: HR data is
  sensitive, and a company buying an HR system will ask this question during evaluation.
- **Full ACL/permission matrix per user** — rejected as over-engineering for MVP; the
  five-role model covers the stated users. Revisit if customers demand custom roles.
