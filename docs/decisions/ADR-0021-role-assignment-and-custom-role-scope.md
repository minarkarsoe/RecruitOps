# ADR-0021 — Who can be given which role, and what a custom role is allowed to mean

**Status:** Accepted (containment) · **Proposed** (custom-role model — decision needed) · **Date:** 2026-10-15
**Relates to:** [ADR-0003](ADR-0003-department-scoping.md), [ADR-0018](ADR-0018-approver-candidate-data-exclusion.md), [ADR-0004](ADR-0004-single-tenant-deployment.md)
**Found by:** driving the running stack against Postgres, not by the test suite

## Context

Milestone 4 (2026-07-30) added a Role Builder: Admins create custom roles with a permission
matrix and assign them to users. ADR-0003 had said the five-role model "covers the stated users
— revisit if customers demand custom roles". No ADR was written when that revisit happened, and
the gap shows in how a role reaches a user.

Authorization in this codebase runs on **two axes that the Role Builder only half-joined**:

1. **The `UserRole` enum** in the JWT role claim. It drives the role policies
   (`RecruitmentStaff`, `InternalUser`, `AdminOnly`), which guard almost every hiring
   endpoint, and `RoleScope`, which decides department scoping (ADR-0003) and candidate-data
   exclusion (ADR-0018). These are the security-critical rules.
2. **The permission matrix** (`[HasPermission]`), which guards the RBAC endpoints themselves
   and drives what the UI shows.

`UserService.AssignRoleToUserAsync` bridged the two with `Enum.TryParse<UserRole>`, which led
to three defects, all reproduced against the dev stack on 2026-10-15:

| # | Request | Result | Severity |
|---|---|---|---|
| 1 | `POST /api/users` with `"role": "SuperAdmin"` | User created as **SuperAdmin** — bypasses every `[HasPermission]` check, so can create Admins with known passwords and rewrite every role | 🔴 Critical |
| 2 | `"role": "0"`, or a custom role with code `"0"` | Also **SuperAdmin**: `TryParse` accepts numeric strings, and `SuperAdmin = 0` | 🔴 Critical |
| 3 | Assign a custom role with **zero** permissions | User logs in as **Recruiter** (the fallback) and reads every posting, requisition and candidate pipeline in the company | 🟠 High |

Defect 3 is structural, not a typo. Even with a correct mapping, a custom role has no answer
to the questions axis 1 asks: *is this person department-scoped? may they reach candidate
data?* Whatever enum value is picked answers those questions on the role's behalf, and the
permission matrix cannot narrow it, because the role policies never consult the matrix. The
handler's seed-role fallback makes it worse: it unions the enum's seed permissions on top of
the custom matrix, so a custom role can never grant *less* than its enum.

## Decision — containment (in force now)

1. **SuperAdmin is never assignable through the tenant directory**, by name, by numeric value,
   or by the system role's id. It is provisioned out of band. A tenant Admin who could grant it
   could grant themselves everything.
2. **Role names are matched exactly**, against a fixed list of assignable enum names. Numeric
   strings are refused; `Enum.TryParse` is not used.
3. **Custom roles can be created and edited, but not assigned** (409, naming this ADR). This
   is fail-closed: the feature becomes inert instead of silently escalating. No customer data
   exists yet (pre-pilot), so no assigned custom roles need migrating.
4. **Custom role codes must start with a letter** (`^[A-Z][A-Z0-9_]{0,63}$`), so a code can
   never look like an enum value. Codes derived from Burmese names fall back to a random
   `ROLE_…` code; the name is what people read.
5. A role assignment by name **clears a stale `RoleId`** instead of leaving the previous
   role's id in place. The evaluator reads `RoleId` first, so a stale id kept granting the old
   role's permissions under the new role's name.

## Decision needed — what a custom role should mean

Options for the product owner. Each is a real model; they differ in what an Admin can express
and in how much of the authorization code must change.

| Option | A custom role is… | Can it narrow a system role? | Cost |
|---|---|---|---|
| **A. Base role + extras** | a system role (chosen explicitly by the Admin at creation) plus extra permissions | No — it can only add | Small: one `BaseRole` column, assignment uses it. Honest about what it is. |
| **B. Scope + matrix** | a permission matrix plus an explicit **data scope** (department-scoped? candidate-data reach?) | Yes | Large: every role-policy endpoint moves to `[HasPermission]`, `RoleScope` reads the role's scope instead of the enum. A second security review. |
| **C. No custom roles** | — (remove the Role Builder's create/assign) | — | Smallest. Matches ADR-0003's original stance until a customer actually asks. |

**Recommendation: A for the pilot, B only if a customer needs a role narrower than any system
role.** A is honest (the UI can say "Recruiter + 2 extra permissions"), small, and keeps the
security-critical rules in `RoleScope` where every existing test pins them. B is the
"real" RBAC, but it rewrites the authorization of every endpoint, and that should be driven by
a real requirement, not by the feature existing.

## Consequences

- The Users page offers only assignable roles; the Role Builder still works for designing roles.
- The permission handler's seed-role fallback (step 3 in `PermissionAuthorizationHandler`) is
  unchanged. Under option A it becomes the defined behaviour; under B it must go.
- `RoleAssignmentEscalationTests` pins the containment: every refusal is paired with a case
  proving the legitimate assignment still works.

## Follow-up — security review, 2026-10-15

An independent review of the containment found no Critical or High issue in it, and these
corrections and additions, all now in place:

- **What SuperAdmin actually reaches.** Earlier text called it "the cross-tenant bypass role". In
  the code it bypasses `[HasPermission]` (the RBAC endpoints) and nothing else: the role policies
  (`RecruitmentStaff`, `InternalUser`, `AdminOnly`) do not include it, and nothing reads a tenant
  override. Defect 1 stays Critical — a SuperAdmin can create an Admin with a known password — but
  "cross-tenant" overstated it.
- **Tokens are re-checked on every request.** Containment only stops *new* grants; a JWT lives 8
  hours, and a deactivated Admin could still create a fresh Admin with it. The account behind a
  token must now exist, be active, and hold the role and SuperAdmin flag the token claims.
- **Directory guards:** a role is required on create (no silent Recruiter); nobody changes their
  own role; the last active Admin cannot be demoted (deactivation already had that floor); a
  SuperAdmin account's role is not managed through the directory; the dev seeder no longer sets
  `IsSuperAdmin` while linking users to roles.
- **The user directory shows the effective set** the API enforces, not the custom role's codes alone.

### Preconditions for options A or B

Before custom-role assignment is re-enabled, whichever option is chosen:

1. **A grantor ceiling.** Today only Admin and SuperAdmin hold `users:create`/`users:update`, so
   "who can grant Admin" is "Admins". A custom role holding `users:update` would be
   Admin-equivalent. A role change must require the target role's permissions to be a subset of the
   actor's effective permissions, and `roles:create`/`roles:update` must not put into a role a
   permission the actor lacks.
2. **Fix the over-broad seed grants** listed in FEATURE-STATUS (HM `move_stage`, HrDirector
   `settings:read`, `users:read`) — under option B they would become real access.
3. **Decide what `Interviewer` is.** It is assignable, but no role policy admits it.
