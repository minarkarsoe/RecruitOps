# challenger_m1_1 — M1 empirical challenge, remit: happy path as Admin

Filed by the Orchestrator from the challenger's text reply.

**Deliverable left in place:** `frontend/internal/src/pages/ApprovalChainsPage.challenger_m1_1.test.tsx`
Product code restored byte-identical after every mutation (`cmp` exit 0; `git diff` = the Worker's
exact 16-line change).

## VERDICT: APPROVE — 0 🔴

## 1. Baseline confirmed, not trusted

`cd frontend/internal && npx vitest run --reporter=basic` → **40 files / 320 tests passed.**
The Worker's reported numbers are honest. (CLAUDE.md's "189 tests across 22 files" is stale — M6.)

## 2. The bug reproduced against pre-fix code — REAL

The mock serves **both** real shapes — `/users` → `{items:[…],page:1,pageSize:20,totalCount:2,totalPages:1}`,
`/users/selectable` → bare array — so the file is a live contract pin, not a mock tuned to the new
call. Chain fixture has 2 steps, opening the `chain.steps.length > 0` gate at `:205`.

```
git show HEAD:…/ApprovalChainsPage.tsx > …/ApprovalChainsPage.tsx
npx vitest run src/pages/ApprovalChainsPage.challenger_m1_1.test.tsx
```
```
Error: Uncaught [TypeError: users.find is not a function]
Error: Uncaught [TypeError: users.map is not a function]
 Test Files  1 failed (1)
      Tests  6 failed (6)
```

**Both** crash sites fire. Note the mechanism: the throw is in the re-render triggered by
`setUsers`, not the first render — so without an ErrorBoundary it presents as a plain "element not
found", which is why this went unnoticed. (Supports M4.)

## 3. The fix proven, driven as Admin

9 tests pass, including: chain with steps renders without throwing; approver **names** render
(`Aung Myat`, `Thida Win`) with GUIDs asserted **absent**; department name resolves; the paged
endpoint is asserted **never** called; a **120-user** list is fully offered; a Burmese approver
name (`ဒေါ်သီတာဝင်း` / `ဌာနအတည်ပြုချက်`) round-trips intact; and the full create flow POSTs exactly
`{name, departmentId:'dept-eng', steps:[{label:'Dept sign-off', approverUserId:'usr-thida'}]}`
and re-renders the new chain with the approver's name.

Independently corroborates `reviewer_m1_1`: the `.items` alternative would have silently truncated
at `PageSize=20` (`UserQueryParameters.cs:5`), so the endpoint choice is **better**, not merely equal.

Reachability checked rather than assumed: `hasPermission` (`lib/auth.ts:169-173`) returns true for
`Admin`, so the Sidebar link renders; `RecruitmentStaff = RequireRole(Admin, HrDirector, Recruiter)`
(`Program.cs:61-62`), so Admin is not newly 403'd.

## 4. Mutation tests — the tests demonstrably fail against broken code

| # | Mutation | Result |
|---|---|---|
| 1 | Whole file reverted to pre-fix (`api<UserListItem[]>('/users')`) | **6/6 failed** with real `TypeError` |
| 2 | Surgical: `.then(setUsers)` → `.then(() => setUsers([]))`, path left correct — no crash, picker silently empty | **4/6 failed**: *"Unable to find an element with the text: Aung Myat"*; dropdown option missing; create flow *"Value 'usr-thida' not found in options"*. The two "does it render at all" tests correctly stayed green — the point being that the **name** assertions carry the weight, not the render assertions. |

## 5. End-to-end check — could not run, substituted honestly

```
docker compose ps
error during connect: open //./pipe/docker_engine: The system cannot find the file specified.
```
**The Docker daemon is down.** Per instructions the challenger did not start or rebuild anything.

Substitute against the real ASP.NET pipeline:
`dotnet test --filter "FullyQualifiedName~UserDirectoryTests"` → **12 passed, 0 failed.**
`An_Admin_Still_Reads_Both` drives both endpoints as Admin (both 200);
`Selectable_Returns_What_A_Picker_Needs_Id_Name_And_Role` deserializes into
`List<SelectableUserDto>`, which only succeeds against a bare JSON array. The paged half is pinned
separately (`UserAccountManagementTests.cs:30-35`). **Both shapes are real — the crash premise is
grounded on the backend, not only in a mock.**

## Findings

### 🟡 1 — A non-selectable approver prints a raw GUID on an existing chain
Same defect `reviewer_m1_1` raised, now **observed**: with chain step
`{sequence:1, approverUserId:'usr-gone', label:'HR Review'}` and `usr-gone` absent from the
selectable list, the cell renders the literal string `usr-gone`. With no chain edit UI (M3) the
Admin can neither identify nor repair that step. Pinned in the test file as `KNOWN GAP: …` with a
comment naming the two expectations to flip when fixed — visible rather than green-by-omission.
Not blocking: requires a deactivated user, and pre-fix code crashed outright in the same scenario.

### 🟡 2 — M1 shipped with no test of its own
The Worker changed three files and added no test. The only coverage of the fix is the two
Challenger files. M4 nominally owns page tests, but the crash M1 fixed is exactly the class a
15-line render test catches.

### 🟡 3 — **A defect in the `/teamwork` gate design, not in M1**
`challenger_m1_2`'s test file landed in the shared tree mid-run: this challenger's full-suite pass
read `42 files / 344 tests, 7 failed` — all failures inside the *other* challenger's file, none
attributable to M1. Excluding it: `41 files / 329 tests, 0 failed` (= 320 baseline + its own 9).
Conversely, its mutation runs briefly reverted `ApprovalChainsPage.tsx` to pre-fix code **while the
other challenger was executing**, so any number that agent reports from that window is suspect.

**The run structure serialises Workers but not Challengers — and a Challenger's job is to mutate
product code.** Orchestrator: this is a real flaw in the command as written and must be fixed
there, not worked around here.
