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
   **Interviewer** is the narrowest: no role-policy screen at all (pipeline, candidates,
   interviews, notes, search, analytics), and only its own departments on permission-guarded
   screens (requisitions). A requisition it is named to approve it can always open and decide.
   An approve-only role is still better on an **Approver** base, which also lists requisitions
   from every department.
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

Widening a role's base role counts as granting everything the role already carries, so it needs
those permissions too; narrowing it does not. Editing anyone — even renaming them — needs reach
over them.

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

### 7.5 Audit log — 🚧 first slice ([ADR-0027](../../decisions/ADR-0027-audit-log.md))
The product's evidence of **who changed access and who signed in**. Retention is **forever**,
with no setting — `design/internal/settings-integrations.html` states it as a guarantee, not a
choice.

**Recorded (first slice):**

| Event | When |
|---|---|
| Signed in / sign-in failed | every attempt through `/api/auth/login` — a failure says why (wrong password, no active account) |
| User created, renamed, role changed, deactivated, reactivated | through the user directory, and the first Administrator created at install |
| Role created, changed, deleted | a change lists what moved: name, base role, active, permissions added and removed, and holders whose access moved with it |
| Access change refused | the grantor ceiling turned a request away (ADR-0021) — someone tried to give what they do not hold |

Each entry keeps **who** (and their name at the time), **what** (a one-line summary plus the
before/after details), **which** user or role, **when**, and **from where** (IP address, and the
trace id the error page shows). It does not keep passwords, tokens, or the content of anything.

**Rules:**
- **Append-only.** Nothing in the product edits or deletes an entry; the database refuses to as well.
- **Written with the change.** An access change and its entry commit together or not at all. A
  failed sign-in or a refusal, where nothing else is saved, is written on its own.
- **Who can read it:** holders of `permission:system:system:audit` — Admin and HR Director by
  default. Users & Roles → **Audit log**, filterable by kind (sign-ins, users, roles, refused) and
  by person or record, newest first.

**Not yet:** exports, reads of candidate data, and requisition decisions (those already have
their own permanent record in the approval chain).

## Entities

- `IntegrationConfig` — provider, credentials ref (secret store — **never** in the DB in plaintext), status
- `RetentionPolicy` — per entity type, retention period, purge behaviour
- `AuditLogEntry` — who did what, to which user or role, when and from where (§7.5; append-only)

## Open questions

- QHRM integration direction: push, pull, or bidirectional? Does it have a public API?
- On "hired", does the employee record get **created in the HRMS automatically**?
- Where do integration credentials live? (Key vault vs. encrypted column — a security decision, needs an ADR.)
- Retention policy vs. legal minimums — who is accountable if a company configures a shorter period than the law allows?
