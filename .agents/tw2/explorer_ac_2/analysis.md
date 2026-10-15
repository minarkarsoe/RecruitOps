# explorer_ac_2 — the frontend approval surface

Filed by the Orchestrator from the explorer's text reply (subagents cannot write files here).
Claims marked **[ORCH-VERIFIED]** were re-checked independently by the Orchestrator.

**Method caveat declared by the explorer:** all findings are static reads — it could not drive
a browser. It also traced the type mismatch by direct file comparison rather than the LSP
warm-up procedure the dispatch specified. The two headline findings were re-verified by the
Orchestrator against the source, so they stand regardless.

## Headline

The **requisition decision flow** (Inbox → detail → Approve/Reject) is real, wired to real
endpoints, and functionally complete. What is broken is the **Approval Chains template admin
screen**, for two independent and both-serious reasons. Either one alone reads to a user as
"the approval chain flow just doesn't work."

## 🔴 Bug 1 — the page crashes for Admin as soon as a chain has steps

**[ORCH-VERIFIED]**

| Side | Reality |
|---|---|
| Frontend | `ApprovalChainsPage.tsx:29` — `api<UserListItem[]>('/users').then(setUsers)` — asserts a **bare array** |
| Backend | `UsersController.cs:29` — returns `ActionResult<PagedResult<UserListItemDto>>`, an **object** `{items, page, pageSize, totalCount, totalPages}` |

`setUsers` therefore stores the paged *object* in a state variable typed as an array. Then:

- `ApprovalChainsPage.tsx:138` — `users.map(...)` inside the create form
- `ApprovalChainsPage.tsx:211` — `users.find(...)`, inside `{chain.steps.length > 0 && ...}` at
  `:201`, so it runs **for every existing chain that has steps**

Both throw `TypeError: users.map/find is not a function`.

**Why this is certainly live, not theoretical:** a chain must exist before any requisition can
be submitted at all — `RequisitionService.cs:157-158` throws "No active approval chain is
configured for this department." So in any environment where Module 1 has ever been exercised,
a chain with steps exists, and this page crashes on render.

**`npm run typecheck` cannot catch this.** The generic `api<T>(...)` is an unchecked assertion
at the call site, not a structural check against the real response. This is a systemic hole in
`lib/api.ts`, not specific to approval chains.

**Fix options:** read `.items` off `PagedResult<UserListItem>` (the type already exists
correctly at `packages/types/src/index.ts:157-163`), or switch to `/users/selectable`
(`UsersController.cs:37-39`) which already returns a bare array and is `RecruitmentStaff`-gated
— the ADR-0019 pattern the repo established for exactly this "lightweight approver picker" case.
The second option also dissolves Bug 2.

## 🔴 Bug 2 — HrDirector is invited in through the front door and 403'd silently

**[ORCH-VERIFIED]** The nav gate and the API gate use **different authorization axes**:

| Layer | Check | HrDirector? |
|---|---|---|
| Sidebar nav link | `Sidebar.tsx:99` — permission code `permission:settings:settings:read` | **Yes** — seeded at `RbacSeedData.cs:109` |
| `/approvalchains` route | `App.tsx:54` — **no `RequirePermission` wrapper at all**, unlike `/users` and `/roles` | passes |
| `GET /approvalchains` API | `ApprovalChainsController.cs:15` — `Policies.AdminOnly` | **No** |
| `GET /users` API | `UsersController.cs:28` — `Policies.AdminOnly` | **No** |
| `AdminOnly` definition | `Program.cs:70` — `p.RequireRole(Roles.Admin)`, a **literal role**, not a permission code | — |

So HrDirector sees the menu item, clicks it, and every fetch 403s. All three fetches are
wrapped in silent catches (`ApprovalChainsPage.tsx:27-29`) — `.catch(() => setChains([]))` and
`.catch(() => {})` — so the screen renders **"No approval chains yet"**. That is
indistinguishable from a genuinely empty state. The user is told nothing went wrong.

This is the repo's documented recurring defect (`NEXT-SESSION.md:176-178`) — a rule applied to
some siblings and not others — but on the **authorization-policy axis** rather than the
department-scoping axis it is usually described for.

**The predicate that would have prevented it already exists and is unused:** `lib/auth.ts:141-143`
defines `isAdmin(role)` with a doc comment naming *"approval chains, departments, the user
directory"* as admin-only surfaces. Neither `Sidebar.tsx` nor `ApprovalChainsPage.tsx` calls it.

**Same bug, same shape, on Departments:** `Sidebar.tsx:87-90` gates on the same permission,
while `DepartmentsPage.tsx:31` calls `GET /departments/admin`, which is `AdminOnly`
(`DepartmentsController.cs:32-34`).

## 🟡 Bug 3 — chain templates cannot be edited or deleted

Symmetrically absent on both sides, so not a mismatch — just unfinished:

- `IApprovalChainService.cs:9-14` — only `GetChainsAsync`, `GetByIdAsync`, `CreateAsync`
- `ApprovalChainsController.cs:16-41` — only `GET`, `GET/{id}`, `POST`
- The page has no edit/deactivate UI

Visible symptom: "I can create a chain but never fix a typo in one."

## What an Approver can actually do today

| Capability | Works? | Evidence |
|---|---|---|
| See Inbox nav link | Yes | `Sidebar.tsx:51-54`, `permission:requisitions:requisitions:approve` held by HrDirector (`RbacSeedData.cs:104`) and Approver (`:153`) |
| Load pending approvals | Yes — real endpoint | `InboxPage.tsx:12` → `RequisitionsController.cs:28-30` → `RequisitionService.cs:46-91`, department-scoped, not mocked |
| See the approval timeline | Yes | `RequisitionDetailPage.tsx:153-184` — sequence, label, decision badge, decided-at, comment |
| Approve/Reject when it is their turn | Yes, correctly gated | `RequisitionDetailPage.tsx:68-71` requires `PendingApproval` + `activeStep.approverUserId === session.userId` + the approve permission |
| Use the nicer drawer/filter UX | **No — dead code** | `features/requisitions/*`, zero importers repo-wide |
| Manage chain templates | **No** | Bugs 1 and 2 above |

## Traps for a Worker

1. **Don't just wrap the route in `RequirePermission`.** The backend is role-literal and the
   frontend is permission-code — they disagree about *who should be allowed in*, not about UI
   polish. Decide product intent first.
2. **Fix `/users` before anything else** — it is a crash, not a rough edge.
3. `features/requisitions/*` is abandoned-in-place, not in progress. Leaving both trees means
   every future approval change has two edit sites and one silently does nothing.
4. `npm run typecheck` passing is not evidence the `/users` call is safe.
5. Backend chain-creation tests only assert Admin-succeeds / Recruiter-forbidden
   (`RequisitionApprovalFlowTests.cs:349-373`) — **HrDirector is never tested**, which is
   precisely the role the sidebar invites in.

## Open Questions

1. **Who should manage chain templates — Admin only, or everyone the Sidebar currently invites
   (incl. HrDirector)?** `ApprovalChainsController.cs:9-12` argues deliberately for Admin-only
   ("editing a chain is equivalent to being able to approve"). The Sidebar disagrees. A Worker
   cannot pick a side without this. **→ for the user.**
2. Delete `features/requisitions/*` or wire it in and retire `pages/*`?
3. Build chain edit/deactivate in this pass, or leave create-only?
4. **UNVERIFIED** — does `SuperAdmin`'s JWT role claim literally equal `"Admin"`? If not,
   SuperAdmin also fails every `AdminOnly` policy. Worth a quick check.
5. **UNVERIFIED** — the sibling `PUT /applications/{id}/profile` 404; explorer confirmed only
   the frontend call at `lib/api.ts:188-192`.
