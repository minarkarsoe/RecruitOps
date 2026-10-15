# ADR-0021 — Who can be given which role, and which enum a custom role holder carries

**Status:** Accepted (SuperAdmin containment) · **Proposed** (custom-role enum — decision needed) · **Date:** 2026-10-15
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

**Custom roles remain assignable** (ADR-0022 depends on it) and their holders still carry
`Recruiter`. That is unchanged — deliberately, so nothing that works today breaks — and is the
decision below.

## Decision needed — what enum a custom role holder carries

| Option | A custom-role holder… | Effect on ADR-0022 roles | Cost |
|---|---|---|---|
| **A. Base role + extras** | carries a system role chosen explicitly by the Admin when building the role | Works; the Admin decides what the holder also reaches through role policies | Small: a `BaseRole` on `Role`, a picker in the Role Builder |
| **B. Finish ADR-0022** | is governed by the matrix everywhere: move the remaining role-policy endpoints to `[HasPermission]`, and give a custom role an explicit data scope for `RoleScope` | Works, and is the "real" RBAC | Large: every remaining endpoint, plus a second security review |
| **C. A no-access enum** | carries an enum no role policy admits | Works for requisitions/chains; the holder reaches nothing else | Smallest, but a custom "Senior Recruiter" can no longer do recruiter work |

**Recommendation: A now, B as ADR-0022 continues.** A is honest — the Role Builder can say
"Recruiter + approve" — and stops the silent escalation without breaking anyone. B is where ADR-0022
was heading anyway; A's base role becomes the data scope B needs.

### Preconditions for A or B
1. **A grantor ceiling.** A role change must require the target role's permissions to be a subset
   of the actor's; `roles:create`/`roles:update` must not grant what the actor lacks.
2. **Fix over-broad seed grants** before any endpoint moves onto permissions.
3. **Decide what `Interviewer` is** — it is assignable, but no role policy admits it.
