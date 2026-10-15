# reviewer_m1_1 — M1 review, remit: authorization and contracts

Filed by the Orchestrator from the reviewer's text reply.

## VERDICT: APPROVE — 0 🔴

## Does M1 widen who can see the user list? **No.**

Traced end to end:

1. **The endpoint's policy is untouched.** `UsersController.cs:38` carries
   `Policies.RecruitmentStaff` = `RequireRole(Admin, HrDirector, Recruiter)` (`Program.cs:60-62`).
   The diff does not touch the backend at all. Nobody gained the ability to *call* it.
2. **The same audience already receives the identical payload from the identical endpoint in
   shipped code.** `ApplicationDebrief.tsx:489` calls `api<SelectableUser[]>('/users/selectable')`,
   gated at `:468` on `isRecruitmentStaff(role)` — the same role set. A Recruiter opening any
   interview debrief already reads every active user's id, displayName and role.
3. **ADR-0019 names this exact consumer as intended**: *"the approval-chain builder wants the
   same shape, and a second copy under a Module 3 name is how the third one gets written."* Its
   Consequences section already booked the cost, and kept email behind `AdminOnly`.

| Role on `/approvalchains` | Before | After |
|---|---|---|
| Admin | 3× 200; picker held the **full directory including email** (`UserListItemDto.Email`) | 200; id/name/role only — **less data crosses the wire** |
| HrDirector / Recruiter | chains 403 (silent), `/users` 403 (silent) → empty picker | chains 403 (**now visible**), `/users/selectable` 200 → populated |
| HiringManager / Approver / Interviewer | all silent | `/users/selectable` **403** — no data |

**Net: the change removes a real disclosure (company-wide email addresses to the Admin's
browser) and adds none.**

Residual, not counted against M1: the "New chain" button (`:81-83`) is ungated, so a Recruiter
can open the form and see a populated dropdown even though `POST` will 403. UI-honesty wart, not
disclosure; it is the nav-gate/API-gate mismatch **M2 owns**.

## Contract correctness — verified against the JSX, not the Worker's claim

Fields read: `:143-144` → `u.id`, `u.displayName`, `u.role`; `:215` → `u.id`, `u.displayName`.
`SelectableUser` (`packages/types/src/index.ts:450-454`) mirrors
`SelectableUserDto(Guid Id, string DisplayName, string Role)`
(`UserListItemDto.cs:23-26`) field-for-field. No `email` is read anywhere on the page, so the
narrower DTO loses nothing.

**The `.items`-unwrap alternative would have been the worse fix** — a finding the Worker did not
make: `UserQueryParameters.PageSize` defaults to **20** (`UserQueryParameters.cs:4`), so
`GET /users` with no query string returns the first 20 users only. On a 60-person company the
approver dropdown would have silently listed a third of the staff with no indication anyone was
missing — *a bug that looks exactly like working software*. `/users/selectable` is unpaged
(`UsersController.cs:41-46`). The Worker's choice was correct for a better reason than it gave.

## Findings

### 🟡 1 — The false empty state survives; it just gets a banner above it
`:28` sets `chains = []` **and** an error, but the empty-state card at `:173-182` gates only on
`chains?.length === 0 && !showForm` — it never consults `error`. So an HrDirector whose
`GET /approvalchains` 403s sees `Request failed (403)` **and**, directly below, *"No approval
chains yet — Create one to enable requisition submission."* while four chains exist in that
database. The page's largest, most confident element positively asserts they do not. This is
exactly the property flagged as the worst part of the original bug. Fix: `&& !error` at `:173`,
or a distinct load-failed state.

### 🟡 2 — A load failure is silently discarded by clicking Cancel
`:163` — `onClick={() => { setShowForm(false); setError(null); }}` — was written when `error`
meant "the create failed". It now also holds load errors. Admin opens page → `/departments` 500s
→ banner shown → clicks "New chain", sees empty dropdown, clicks "Cancel" → **banner gone
permanently**, no refetch. A healthy-looking screen over a silently broken department list: the
pre-M1 failure mode, reintroduced through shared state.

### 🟡 3 — Submit errors moved away from the submit button
The banner moved from inside the Card (old `:90`, above the form) to `:86`, above `<header>`.
With four or five steps the submit button at `:160` is below the fold, so a POST that 403s or
409s writes its message off-screen with no scroll-into-view — the button re-enables and, from the
user's seat, nothing happened.

### 🟡 4 — Deactivated approvers now render as a raw GUID
`/users/selectable` filters `u.IsActive` (`UsersController.cs:43`), but
`ApprovalChainService.CreateAsync` validates only that the approver *exists* (`:53-55`), and
nothing deactivates a chain when its approver is deactivated. So the lookup at `:215` misses and
falls back to the GUID: *"Finance sign-off · 8f3c1d2e-4a5b-…"*. Not a regression — that line threw
a `TypeError` before — but a new visible defect that will read as data corruption to whoever hits
it first.

### 🟢 5 — The three fallback strings are unreachable
`apiFetch` throws only `ApiError extends Error` (`lib/api.ts:23-28`), so `e instanceof Error` is
always true and `'Could not load approval chains.'` etc. never render. Users see
`Request failed (403)` (`lib/api.ts:159-161`) — identical for all three, naming neither the
resource nor the remedy. The strings that would say *which* fetch failed are exactly the ones
that never run.

### 🟢 6 — Pattern divergence from the one existing consumer
`ApplicationDebrief.tsx:487-492` guards the same call with `if (!canManage) return;` and a comment
explaining why. `ApprovalChainsPage.tsx:31` fetches unconditionally. Defensible, but the sibling
is the pattern; worth aligning when M2 gives this page a real capability predicate.

## Not raised, and why
Docs drift on `SelectableUserDto`'s "interview panel" comments (three sites) — ADR-0019
anticipated the second consumer, so stale rather than false; **M6 owns docs**. Route guard,
`[HasPermission]`, chain PUT/DELETE, page tests, `features/requisitions/*` — M2/M3/M4/M5 by design.

No `any`, no nullable suppression, no architecture-boundary crossing, no role literal in a service.
