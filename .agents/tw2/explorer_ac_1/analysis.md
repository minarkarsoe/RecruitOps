# explorer_ac_1 — the backend approval chain lifecycle

Filed by the Orchestrator from the explorer's text reply (subagents cannot write files here).
Claims marked **[ORCH-VERIFIED]** were re-checked independently by the Orchestrator.

## Summary

The backend lifecycle is **fully built, correctly wired, and verified passing**. The explorer
ran `dotnet test --filter RequisitionApprovalFlowTests` against real compiled code — **17/17
green** — rather than trusting the docs. No `NotImplementedException`, no stub, no dead DTO
anywhere in this path.

Two findings, neither of which is "the backend logic is missing".

## Chain of causation

| Link | State | Evidence |
|---|---|---|
| Domain model (`ApprovalChain`, `ApprovalChainStep`, `RequisitionApproval`, enums) | WORKS | `ApprovalChain.cs:1-16`, `ApprovalChainStep.cs:1-20`, `RequisitionApproval.cs:1-24`, `ApprovalDecision.cs:1-9`, `RequisitionStatus.cs:1-11`. Template vs. per-requisition snapshot correctly separated. |
| `ApprovalChainService` | WORKS, no stubs | `ApprovalChainService.cs:1-91`. `CreateAsync` validates every approver exists (`:53-55`) before creating the chain. |
| DI registration | WORKS | `DependencyInjection.cs:57-58` |
| **Submit stamps out approval rows** | WORKS | `RequisitionService.cs:151-179` — picks the **department-specific chain first, falls back to company-wide** (`:152-155`); throws if no chain or no steps (`:157-166`); one `RequisitionApproval` per step, `Decision = Waiting` (`:168-179`). **[ORCH-VERIFIED]** |
| Decision endpoint + DTO | WORKS, not dead | `RequisitionsController.cs:79-91`; `ApprovalDecisionRequest` referenced by 10 files incl. 6 test files |
| Sequential enforcement | WORKS | `RequisitionService.cs:203,205`; test `A_Later_Approver_Cannot_Jump_The_Queue` passes |
| Status forward on approve | WORKS | `RequisitionService.cs:220-224` |
| Status on reject | WORKS — **terminal, no revise-and-resubmit** | `:215-219`. Any single reject → `Rejected`, which is terminal per `RequisitionStatus.cs`. `UpdateAsync` permits edits only while `Draft` (`:242-244`), so a rejected requisition can never be fixed and resubmitted — a new Draft must be raised. **Open question for the user.** |
| Department scoping on decide (ADR-0003) | WORKS — **approver deliberately crosses departments** | `DecideAsync` (`:189-230`) has no `_access.CanAccessAsync` call; access is solely "are you the named approver on the current waiting step". Intentional and tested: `ApproverReachTests.cs:139-170`, documented at `Policies.cs:9-13`. |
| Migration | WORKS | `20260727101933_Module1Requisitions.cs`, 5 `CreateTable` calls |

## 🔴 Finding A — the `GET /api/users` contract drift (root cause of the visible symptom)

**[ORCH-VERIFIED]** Same bug `explorer_ac_2` found, but traced to its origin commit:

`GET /api/users` was repurposed for the new User Directory feature in commit **`85c3e39`
(2026-07-30)**, changing its response from a bare array to `PagedResult<UserListItemDto>`
(`PagedResult.cs` created fresh in that commit). The one earlier consumer —
`ApprovalChainsPage.tsx`, last touched in `24e3c9c` — was never updated and still does
`api<UserListItem[]>('/users')`.

`apiFetch<T>` performs an **unchecked `as T` cast** (`frontend/internal/src/lib/api.ts:103`),
so this typechecks clean and fails only at render:
`TypeError: users.find/map is not a function`.

**There is no `ErrorBoundary` anywhere in `frontend/internal/src`** (grep: zero matches), so
it fails as a **blank page with no diagnostic**.

The explorer notes this is half-backend/half-frontend and flags it for the Orchestrator to
route — a strictly-backend Worker should not be silently patching a `.tsx` file.

## 🔴 Finding B — the Dynamic RBAC permissions for approval are seeded but never enforced

**[ORCH-VERIFIED]** This is the systemic one, and it is exactly the capability the user asked
for.

| Fact | Evidence |
|---|---|
| `permission:requisitions:requisitions:approve` is seeded | `RbacSeedData.cs:23` |
| It is granted to HrDirector and Approver | `RbacSeedData.cs:104`, `:153` |
| `[HasPermission]` appears in `RequisitionsController` | **0 times** |
| `[HasPermission]` appears in `ApprovalChainsController` | **0 times** |
| Both still gate on legacy static policies | `Policies.InternalUser` / `Policies.AdminOnly` = `RequireRole(...)` against the fixed `UserRole` enum, `Program.cs:70` |
| Which controllers *do* use `[HasPermission]` | only `AiController`, `PermissionsController`, `RolesController`, `UsersController` |

**Consequence:** granting or revoking the approve permission through the Role Builder has **no
effect whatsoever** on who can approve. A custom role can never reach these endpoints no matter
what it is granted. The permission is decorative.

The explorer is careful to note this is app-wide debt rather than Module-1-specific, and that
CLAUDE.md currently scopes `[HasPermission]` to only those four controllers — so it flagged it
as a contributing factor, not the root cause of the crash. **The Orchestrator disagrees on
priority**: the user's stated goal is configurable, department-aware approval authority, which
is precisely what Finding B blocks. It is promoted to a milestone of its own.

## What a Worker needs to do

1. **Fix the contract mismatch.** Prefer copying the existing `GET /users/selectable` pattern
   (`UsersController.cs:37-53` — bare array, `SelectableUserDto`, already the ADR-0019 picker
   pattern) over unwrapping `.items`.
2. **Add an ErrorBoundary to `frontend/internal`.** This bug class recurs and currently renders
   as a blank page with zero signal.
3. **Add a page-level test for `ApprovalChainsPage`**, following `InterviewDetailPage.test.tsx`'s
   `vi.mock('../lib/api')` pattern — a render test would have caught this instantly.
4. **Decide on the `[HasPermission]` migration** for Module 1, or explicitly document that it is
   intentionally still on static policies. Seeded-but-dead permission codes must not sit there
   implying control they do not have. **Security-relevant — CLAUDE.md requires `security-reviewer`.**

## Open Questions

1. **Is a rejected requisition meant to be revisable and resubmittable?** Currently terminal —
   a new Draft must be raised. Nothing in the ADRs or the module doc plans a revise loop. If the
   user's "not finished" actually means "I can't fix and resubmit a rejection", that is a larger
   product gap than the UI crash. **→ for the user.**
2. Should one Worker own both the backend and the `.tsx` fix, or split per CLAUDE.md's
   "agree on the API contract first" rule?
3. Is the `[HasPermission]` migration in scope? It touches every requisition/approval
   endpoint's authorization.
4. Not checked: whether other components built before pagination existed consume another
   paginated endpoint the same way. Only other `/users` call site found is
   `ApplicationDebrief.tsx:489`, which correctly uses `/users/selectable` — unaffected.
