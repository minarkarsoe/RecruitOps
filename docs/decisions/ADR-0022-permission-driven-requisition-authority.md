# ADR-0022 — Approval authority is permission-driven, not a hardcoded role literal

**Status:** Accepted · **Date:** 2026-08-15 · **Related:** [ADR-0003](ADR-0003-department-scoping.md),
[ADR-0018](ADR-0018-approver-candidate-data-exclusion.md)
**Found by:** `explorer_ac_1`, teamwork run `tw2` (`.agents/tw2/explorer_ac_1/analysis.md`, Finding B),
promoted to a milestone by the Orchestrator because it is the user's actual request.

## Context

The user asked, in their own words: *"We can config who can do the approval chain base on who
request or which department."* Module spec 1.3, *Dynamic Approval Workflow* (`docs/product/modules/
01-job-requisition-approval.md:20-23`), already says this: *"The chain is configurable per company,
not hard-coded."*

The Dynamic RBAC system (`RbacSeedData.cs`) already seeds exactly the permissions this needs —
`permission:requisitions:requisitions:{read,create,update,delete,approve}` and
`permission:settings:settings:{read,update}` — grants them to roles, and displays and edits them in
the Role Builder. But `[HasPermission]` appeared **zero times** in `RequisitionsController` or
`ApprovalChainsController`. Both gated on `Policies.InternalUser` / `Policies.AdminOnly`, i.e.
`RequireRole(...)` against the fixed five-value `UserRole` enum (`Program.cs:66-70`).

The consequence: granting or revoking `requisitions:approve` through the Role Builder changed
nothing. A tenant could build a custom role, grant it `requisitions:approve`, assign a user to it,
and that user would still 403 on `POST /{id}/decision` — the role literal, not the permission, was
the real gate. The seeded codes were decorative.

## Decision

Replace the controller-level role-literal policy on both controllers with a bare `[Authorize]`
(authentication only) plus a per-action `[HasPermission("permission:...")]` attribute, matching the
pattern already used by `UsersController` and `RolesController`. `HasPermissionAttribute` extends
`AuthorizeAttribute`, so each action still requires an authenticated principal; it additionally
requires the named permission, evaluated by `PermissionAuthorizationHandler` against the caller's
actual `RoleId` (custom or system) via `PermissionEvaluator`.

### Endpoint → permission mapping

`RequisitionsController`:

| Action | Permission |
|---|---|
| `GET /` (list) | `permission:requisitions:requisitions:read` |
| `GET /inbox` | `permission:requisitions:requisitions:approve` |
| `GET /{id}` | `permission:requisitions:requisitions:read` |
| `POST /` (create) | `permission:requisitions:requisitions:create` |
| `PUT /{id}` | `permission:requisitions:requisitions:update` |
| `POST /{id}/submit` | `permission:requisitions:requisitions:update` |
| `POST /{id}/decision` | `permission:requisitions:requisitions:approve` |
| `POST /{id}/cancel` | `permission:requisitions:requisitions:update` |

`ApprovalChainsController`:

| Action | Permission |
|---|---|
| `GET /`, `GET /{id}` | `permission:settings:settings:read` |
| `POST /` | `permission:settings:settings:update` |

`cancel` uses `update` rather than `delete`: the module doc (`:46-49`) says a requisition is
withdrawn by the person who raised it, and `RequisitionService.IsOwnerOrCompanyWide` already
enforces that at the service level — the permission is a coarse gate that a requester (who holds
`update`, not `delete`) must still pass, not the fine-grained authority itself.

## Why role literals were insufficient

A `RequireRole` policy is checked against the fixed `UserRole` enum burned into `ClaimTypes.Role` at
login. A custom role built through the Role Builder has no seat in that enum — `AssignRoleToUserAsync`
falls back its legacy `Role` field to `Recruiter` when the custom role's code doesn't parse, purely
so the JWT has *some* value there. No amount of permission-granting through the Role Builder could
ever change who `RequireRole(Admin, HrDirector, Recruiter, HiringManager, Approver)` lets through. The
permission system and the role-literal system were two authorization mechanisms that disagreed, and
the one actually wired to the endpoints — the literal — always won.

## Behaviour changes (intended)

1. **Recruiter keeps create and update on requisitions, and the seed was corrected to say so.**
   The first draft of this ADR recorded the opposite: `Policies.InternalUser` let all five of its
   roles reach every action, while the seed granted Recruiter `requisitions:read` only, so making
   the permission model authoritative would have removed a capability recruiters had in practice.
   The product owner confirmed recruiters must be able to raise requisitions, so
   `requisitions:create` and `requisitions:update` were added to the Recruiter role in
   `RbacSeedData.cs` rather than the behaviour being allowed to change.

   **`create` and `update` are granted as a pair deliberately.** The flow is create → edit the
   draft → submit, and both `PUT /{id}` and `POST /{id}/submit` are gated on `update`. Granting
   `create` alone would let a recruiter raise a requisition they could then neither correct nor
   submit — a worse outcome than not being able to raise one, and the kind of half-applied rule
   this repo has shipped before.

   `requisitions:approve` is **not** granted to Recruiter: raising headcount and approving it are
   different authorities, and the approval chain decides the second one.

   This episode is itself an argument for the ADR: under the old role literal, the disagreement
   between "what `InternalUser` allowed" and "what the seed granted" was invisible, because
   nothing consulted the seed. Making permissions authoritative forced the question to be asked
   and answered explicitly.
2. **`GET /inbox` narrows to roles holding `requisitions:approve`.** Recruiter and HiringManager lose
   access. Verified empirically before the change: their inbox already returned `[]` under the old
   gate (no waiting step is ever assigned to them), so nothing they could previously see disappears.
3. **`GET /approvalchains` widens to any role holding `settings:read`, including `HrDirector`.** This
   also fixes a real, independently-discovered defect: `Sidebar.tsx:99` already showed the "Approval
   chains" nav item to anyone with `settings:read`, while the API demanded `Admin` — so HrDirector saw
   a link that silently 403'd into an empty page. The nav gate and the API gate now agree.
4. **Creating a chain still effectively requires `Admin`** (only `Admin` is seeded with
   `settings:update`), but the requirement is now expressed as a grantable permission, not a role
   literal — a tenant can extend chain-authoring to `HrDirector` or a custom role without a code
   change.

### A second-order consequence found while testing this change

Roles that hold `requisitions:approve`/`read` but **not** `requisitions:update` — `Approver` in the
seed data — previously reached `PUT /{id}`, `POST /{id}/submit`, and `POST /{id}/cancel` (gated only
by `Policies.InternalUser`) and were stopped by `RequisitionService.IsOwnerOrCompanyWide`, a
per-resource 404. They are now stopped at the policy layer with a blanket 403 before the service is
ever called, since those three actions require `requisitions:update`. The underlying guarantee — an
Approver cannot submit, edit, or cancel a requisition it does not own — is unchanged and, if anything,
enforced earlier and unconditionally rather than per-resource. `RequisitionApprovalFlowTests.cs` was
updated to assert the new (403) status for the four tests this affects, with comments recording why.

## Department scoping remains the security-critical filter

ADR-0003's position is unchanged by this ADR: the department predicate (`IDepartmentAccess.
CanAccessAsync`, applied explicitly inside `RequisitionService`) is what actually decides whether a
caller can touch a *specific* requisition. `[HasPermission]` is a coarse, resource-independent gate
layered in front of it — it decides whether the caller may attempt the action class at all, not
whether they may act on any particular row. Nothing in this change removes, weakens, or bypasses a
`CanAccessAsync` call; `DecideAsync` in particular still has none (intentional and pre-existing —
approval chains cross departments, see ADR-0018), and a regression test
(`Permission_Gate_Does_Not_Bypass_Department_Scoping_On_Create`) asserts a HiringManager who holds
`requisitions:create` still cannot create a requisition in a department they do not own (404, not
403 — ADR-0003's no-oracle rule).

## Consequences

- The Role Builder now genuinely controls requisition and approval-chain authority — the user's
  stated goal.
- Any tenant wanting the exact pre-migration behaviour (all five internal roles reaching every
  requisition action) can reconstruct it by granting the relevant permissions per role; nothing here
  removes that option, it just stops being the only option.
- `Policies.InternalUser` and `Policies.AdminOnly` are untouched and still used by other controllers
  not in scope for this change (out of scope per `PROJECT.md`: *"The app-wide `[HasPermission]`
  migration beyond Module 1's controllers"*). `InterviewsController`'s analogous gap for the
  `Interviewer` role is recorded separately as milestone M5.3.

## Alternatives rejected

**Add a `HasPermission` check inside the service alongside the existing role check, leaving the
controller policy as-is.** Cheaper, and reintroduces exactly the "two systems disagree" bug this ADR
fixes — a future endpoint added to the controller would inherit the role literal by default, not the
permission, and the seeded codes would stay decorative for it.

**Give `Admin` a blanket bypass in `PermissionAuthorizationHandler`, alongside the existing
`SuperAdmin` bypass, and rely on that to keep `Admin`'s behaviour identical.** Rejected: `Admin` is
seeded with every permission except `system:manage`, so it passes every `[HasPermission]` check on
its own merits. A bypass would hide that fact and make `Admin`'s access silently untestable against
the real permission set.

---

## Amendment — the claim fallback had to go too (found by security review)

The first cut of this ADR shipped the controller mapping and stopped there. The
`security-reviewer` pass CLAUDE.md mandates for authorization changes rejected it, and was right.

**The hole.** `PermissionAuthorizationHandler` asked `PermissionEvaluator` for the caller's
permissions and, **if the answer was no, did not return**. It fell through to a "role-based claim
fallback for system roles" which matched the JWT's role claim against the static
`RbacSeedData.GetSystemRoles()` list and granted on a hit.

That mattered because of a second, unrelated behaviour: `AssignRoleToUserAsync`
(`UserService.cs:318-325`) sets `user.Role = UserRole.Recruiter` for **every** custom role. A
custom role's generated code never parses as a `UserRole`, and `RoleService` rejects names that
collide with system role codes — so that `else` branch is not an edge case, it is the only
reachable path for the entire Role Builder feature. That literal becomes the JWT role claim
(`JwtTokenService.cs:45`).

Compose the two and the Role Builder could grant permissions but **never withhold them**: a
read-only custom role's users were topped up to Recruiter's entire seeded set. Adding
`requisitions:create` and `requisitions:update` to Recruiter — this ADR's own change, made so
recruiters could raise requisitions — is what turned a latent flaw into "any custom role can create
and edit requisitions regardless of configuration". Precisely the guarantee this ADR claims to
deliver, inverted.

**The fix, and why not simply deleting the block.** Deleting it outright broke **79 tests**, which
was informative rather than merely inconvenient: the test harness authenticates with a role header
and no user id, so `sub` never parses, the evaluator is never consulted, and the fallback was
carrying those cases legitimately. The distinction that matters is **"denied" versus "unknown"**:

- identity resolves → the database is authoritative, and its answer is final **including "no"**
- identity does not resolve → the evaluator was never asked, so the seeded system-role fallback
  applies

That mirrors `PermissionEvaluator.cs:99`, which already applied the seed only when the resolved
permission set is *empty*. The handler's copy simply lacked that guard.

**A second hole closed by the same change.** `PermissionEvaluator.cs:45` requires `u.IsActive`, so a
deactivated user resolves to no permissions — and the fallback used to rescue them from the seed. A
deactivated user's still-valid JWT therefore kept working against every permission-gated endpoint.
It no longer does. Four existing tests were driving requests from deactivated or cross-tenant
clients and passing because of this; they asserted the right rules through the wrong callers and
have been corrected to use callers that genuinely hold the authority.

**Pinned by:** `A_Custom_Role_Denied_A_Permission_Is_Actually_Denied_Not_Floored_To_Recruiter` and
`A_Resolvable_Identity_Denied_By_The_Database_Is_Not_Rescued_By_Its_Role_Claim`, plus
`A_System_Role_Claim_Still_Grants_When_No_Identity_Can_Be_Resolved` so the fix cannot later be
"fixed" into a blanket denial. Restoring the fall-through turns the first two red; verified.

**Known and accepted, not fixed here:** aliasing every custom role to `UserRole.Recruiter` in
`UserService.cs:324` is still misleading — it is now inert for authorization, but a permission-less
sentinel would be honest. Tracked separately; it is a data-modelling fix, not a security one, now
that the claim is no longer load-bearing.
