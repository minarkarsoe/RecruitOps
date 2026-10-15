# Module 7 — Settings & Integrations

**Status:** 🚧 Partial — RBAC + multi-tenancy are **built**; integrations not started.
**Priority:** RBAC done; integrations later.

## Purpose

Per-company configuration: who can see what, and how the system connects to the
company's existing HR stack.

## Features

### 7.1 Role-Based Access Control (RBAC) — ✅ Built
Restrict system access and visibility **according to each user's role**.
See [architecture/auth-and-tenancy.md](../../architecture/auth-and-tenancy.md).

> Note: the currently-implemented role set is the **agency** one and must be revised
> for in-house — see the migration plan.

#### Custom roles: base role + permissions ([ADR-0021](../../decisions/ADR-0021-role-assignment-and-custom-role-scope.md), option A)

A custom role has two parts, both chosen by whoever builds it in the Role Builder:

1. **A base role** — the system role whose *reach* its holders get: which screens and data
   they can open at all (pipeline, candidates, interviews, analytics…), and how they are scoped
   (a Hiring Manager base sees only its own departments; an Approver base sees no candidate data).
   One of **HR Director, Recruiter, Hiring Manager, Approver, Interviewer**. Required; never
   Admin or SuperAdmin — admin reach is given by assigning the Admin system role, openly.
   **Interviewer** reaches no role-policy screen at all, so an Interviewer-based role is "the
   permissions I tick, and nothing else" — the right base for, say, an approve-only role.
2. **Permissions** — the matrix, which is the whole answer for permission-guarded endpoints
   (the RBAC screens, requisitions, approval chains — ADR-0022). Nothing is topped up from the
   base role's own matrix.

Changing a role's base role, or deactivating it, takes effect for its holders immediately:
their sessions are re-checked on the next request. A **deactivated** role grants nothing on
either axis — its holders keep their accounts but reach only what an Interviewer does.

**Nobody grants what they do not hold** (the grantor ceiling). Whoever builds a role, edits
one, reactivates one, assigns a role to a user (system or custom), or reactivates a user can
only:
- grant permissions they hold themselves — so an Admin cannot grant `system:manage`, which
  only SuperAdmin holds; and
- give reach within their own: Admin ⊇ HR Director ⊇ Recruiter ⊇ {Hiring Manager, Approver}
  ⊇ Interviewer. A non-Admin can therefore never create or assign an Admin.

SuperAdmin is exempt. By default only Admins manage users and roles, so for everyone else this
matters once an Admin delegates — e.g. a custom "HR Lead" role holding `users:create`.

### 7.2 HRMS & Payroll Integrations — ⬜
Connect data to the company's core **HRMS (e.g. QHRM)** and payroll systems.

### 7.3 Email & Calendar Sync — ⬜
Full integration with **Microsoft 365** or **Google Workspace**. Required by Module 3
(scheduling) and Module 4 (offer emails).

### 7.4 Data Security & SLA Configuration — ⬜
Per-company configuration of **system recovery objectives**, **data retention policy**,
and security settings, in line with that company's SLA.

## Entities

- `IntegrationConfig` — provider, credentials ref (secret store — **never** in the DB in plaintext), status
- `RetentionPolicy` — per entity type, retention period, purge behaviour
- `AuditLog` — who did what, when (needed for both security and SLA claims)

## Open questions

- QHRM integration direction: push, pull, or bidirectional? Does it have a public API?
- On "hired", does the employee record get **created in the HRMS automatically**?
- Where do integration credentials live? (Key vault vs. encrypted column — a security decision, needs an ADR.)
- Retention policy vs. legal minimums — who is accountable if a company configures a shorter period than the law allows?
