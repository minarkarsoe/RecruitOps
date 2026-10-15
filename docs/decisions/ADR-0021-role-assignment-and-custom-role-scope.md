# ADR-0021 — Who can be given which role, and which enum a custom role holder carries

**Status:** Accepted — SuperAdmin containment, and **option A** for the custom-role enum (chosen by the product owner 2026-10-15) · **Date:** 2026-10-15
**Relates to:** [ADR-0022](ADR-0022-permission-driven-requisition-authority.md), [ADR-0003](ADR-0003-department-scoping.md), [ADR-0018](ADR-0018-approver-candidate-data-exclusion.md), [ADR-0004](ADR-0004-single-tenant-deployment.md)
**Found by:** driving the running stack against Postgres, then two security reviews

## Context

Authorization runs on **two axes**:

1. **The `UserRole` enum** in the JWT role claim. It drives the role policies (`RecruitmentStaff`,
   `InternalUser`, `AdminOnly`) and `RoleScope` (department scoping, ADR-0003; candidate-data
   exclusion, ADR-0018).
2. **The permission matrix**, enforced by `[HasPermission]`. Since ADR-0022 that covers the RBAC
   endpoints, requisitions and approval chains — and the evaluator's answer is final, with no
   role-claim fallback.

`UserService.AssignRoleToUserAsync` bridged them with `Enum.TryParse<UserRole>`, which accepts every
enum name **and any numeric string**. Reproduced on 2026-10-15:

| # | Request | Result | Severity |
|---|---|---|---|
| 1 | `POST /api/users` with `"role": "SuperAdmin"` | User created as **SuperAdmin** | 🔴 Critical |
| 2 | `"role": "0"`, or a custom role coded `"0"` | Also **SuperAdmin** (`SuperAdmin = 0`) | 🔴 Critical |
| 3 | Assign any other custom role | The enum becomes **Recruiter** | 🟠 High — see below |

SuperAdmin bypasses every `[HasPermission]` check and, on develop, may override its tenant with
`X-Tenant-Id` (`SuperAdminTenantOverrideMiddleware`) — so a tenant Admin who could mint one reached
other tenants' data. That is what made 1 and 2 Critical.

**Defect 3 is structural.** ADR-0022 made the custom matrix authoritative for requisitions and
approval chains, and removed the handler's Recruiter floor there. But the enum still says
Recruiter, and most of the hiring surface — applications, candidates, interviews, job postings,
notes, search, analytics — is guarded by role policies that read the enum. So a custom role built
to *only* approve requisitions gives its holder the whole pipeline, company-wide.

## Decision — in force

1. **SuperAdmin is never assignable through the user directory** — by name, by numeric value, or
   by the system role's id. It is provisioned out of band.
2. **System role names are matched exactly** against an explicit list; numeric strings are refused.
3. **A custom role's code is never parsed as an enum.** Codes must also start with a letter
   (`^[A-Z][A-Z0-9_]{0,63}\z`); codes derived from Burmese names fall back to a random `ROLE_…` code.
4. **Directory guards:** a role is required on create; nobody changes their own role; the last
   active Admin cannot be demoted; a SuperAdmin account's role is not managed here; the seeder links
   users to roles without ever setting `IsSuperAdmin`.
5. **Every request re-checks the token's account** (exists, active, same role and SuperAdmin flag),
   so a role change or deactivation takes effect immediately rather than after 8 hours.
6. **A custom role is its holder's whole permission matrix.** The evaluator's seed fallback (for
   an enum with no `RolePermission` rows) serves system roles only: a custom role that is
   deactivated, emptied, or missing grants nothing — never the placeholder enum's matrix.

**Custom roles remain assignable** (ADR-0022 depends on it). What enum their holders carry was
the open question below, now decided.

## Options considered — what enum a custom role holder carries

| Option | A custom-role holder… | Effect on ADR-0022 roles | Cost |
|---|---|---|---|
| **A. Base role + extras** | carries a system role chosen explicitly by the Admin when building the role | Works; the Admin decides what the holder also reaches through role policies | Small: a `BaseRole` on `Role`, a picker in the Role Builder |
| **B. Finish ADR-0022** | is governed by the matrix everywhere: move the remaining role-policy endpoints to `[HasPermission]`, and give a custom role an explicit data scope for `RoleScope` | Works, and is the "real" RBAC | Large: every remaining endpoint, plus a second security review |
| **C. A no-access enum** | carries an enum no role policy admits | Works for requisitions/chains; the holder reaches nothing else | Smallest, but a custom "Senior Recruiter" can no longer do recruiter work |

**Chosen: A now, B as ADR-0022 continues.** A is honest — the Role Builder says "Recruiter base +
these permissions" — and stops the silent escalation without breaking anyone. B is where ADR-0022
was heading anyway; A's base role becomes the data scope B needs. C survives inside A: an
Interviewer base *is* the no-access enum.

## Decision — option A (2026-10-15)

1. **`Role.BaseRole`.** Every custom role has one, chosen when it is built: `HrDirector`,
   `Recruiter`, `HiringManager`, `Approver` or `Interviewer`. **Never `Admin` or `SuperAdmin`**:
   admin reach is given by assigning the Admin system role, where the user directory shows it.
   System roles have none — their code is their enum.
2. **A holder's enum is the role's base role while the role is active, and `Interviewer` while
   it is not.** `Interviewer` is the enum no role policy admits, so a deactivated role grants
   nothing on either axis ("in force" item 6 covers the matrix). Holders are re-synced whenever the
   base role or the active flag changes; the session check ("in force" item 5) then retires their
   tokens.
3. **Existing custom roles migrate to a `Recruiter` base** — what their holders carried already,
   so nothing changes until an Admin chooses otherwise. Holders of an inactive custom role move to
   `Interviewer`.
4. **The grantor ceiling** (precondition 1, below). Creating, editing or reactivating a role, and
   assigning a role to or reactivating a user, require the actor to hold
   - every permission being *added* (on assignment, reactivation, and widening a role's base
     role — a new base the old one does not cover — every permission the role carries), and
   - reach over the enum being given: `Admin ⊇ HrDirector ⊇ Recruiter ⊇ {HiringManager, Approver}
     ⊇ Interviewer` (`RoleScope.Covers`). The order follows the role policies — `AdminOnly`,
     `RecruitmentStaff`, `InternalUser` — plus the two scopes: HrDirector's company-wide
     requisition authority over Recruiter, HiringManager's department scope, Approver's
     candidate-data exclusion. `RoleScopeCoversTests` pins it against the live policies.

   Managing — editing or deleting a role, editing a user (a rename too: it changes their
   `@mention` handle), deactivating or reactivating a user — also requires covering the *current*
   reach of that role or user: demoting is not a grant, but a delegate should not be able to
   demote or lock out the people above them.

   SuperAdmin is exempt. A refusal is a 403. The actor's role and SuperAdmin flag are read from the
   token like every other authorization decision (safe because decision 5 of "in force" re-checks
   both against the database on every request); what the actor holds comes from the evaluator.
5. **Interviewer** (precondition 3) is the role with **no standing reach**, and the narrowest on
   every axis: no role policy admits it — so no interview, candidate or note endpoint either,
   panel or not — and `RoleScope` treats it as department-scoped *and* excluded from candidate
   data. (If `InternalUser` ever admits Interviewer, the panel rule of ADR-0017 §4 is what those
   two flags would then enforce; re-audit every policy-only endpoint first.) Any approver named on
   a requisition's chain may read that requisition whatever their scope (ADR-0003's "an approver
   must see what they approve"; `RequisitionService.IsNamedApproverAsync`).
   The first build left it company-wide in `RoleScope` — harmless while only role policies guarded
   anything, but requisitions are permission-guarded, so an Interviewer-based role with
   `requisitions:read` saw every department, and a department-scoped delegate could hand that out
   (security review of option A, finding 1). Approve-only custom roles are best on an Approver base.

   Holders are judged one by one as well as by the role's base: an edit that would move a holder
   off another enum (old data) needs reach over them, and lifting one past their reach counts as
   widening. The ceiling judges a user flagged `IsSuperAdmin` as SuperAdmin, whatever their enum.

**Accepted residuals** (security review of option A): the ceiling bounds each grant by the
grantor's own matrix, so two delegates with different matrices can between them build a role
neither could alone; a delegate can deactivate a *peer* (equal reach) whose matrix is richer, and
cannot undo it; a role deactivated in the milliseconds between another admin's assignment read and
save leaves that one holder on the base enum (no permissions — the evaluator still refuses).
Option B, or a separation-of-duties rule, is where those close.

Precondition 2 (over-broad seed grants) applies to option B — moving endpoints onto permissions —
and stays open with it.

### Preconditions for A or B (as written before the decision)
1. **A grantor ceiling.** A role change must require the target role's permissions to be a subset
   of the actor's; `roles:create`/`roles:update` must not grant what the actor lacks.
2. **Fix over-broad seed grants** before any endpoint moves onto permissions.
3. **Decide what `Interviewer` is** — it is assignable, but no role policy admits it.
