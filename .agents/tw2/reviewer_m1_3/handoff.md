# reviewer_m1_3 — M1 RE-GATE review (after 2 remediation loops)

Filed by the Orchestrator. **[ORCH-VERIFIED]** = re-checked directly against the source.

## VERDICT: REJECT — 1 🔴

## 🔴 1 — The error split made a submit failure UNRENDERABLE when the chain load failed

**[ORCH-VERIFIED — all five facts checked directly]**

- `ApprovalChainsPage.tsx:55` — `const error = chainsError ?? formError ?? auxError;`
- `chainsError` is set only at `:61` and is **never cleared anywhere** — no retry, no reset
- `formError` has **no independent render site**; it reaches the DOM only through that computed `error`
- The diff **removed** the in-form error render: `- {error && <p role="alert" …>{error}</p>}`
- `RolesPage.tsx:13` + `:306-310` is the **existing repo pattern** — a page-level `error` *and* a
  separate `formError` rendered inside the form, never collapsed

**Therefore: once the chain load has failed, no form error can ever reach the screen for the life
of that page.**

Default-install path, not a corner case:

1. `RbacSeedData.cs:109` grants HrDirector `permission:settings:settings:read`; the nav item is
   gated on exactly that, and `App.tsx:54` has no route guard — a seeded HrDirector reaches the page.
2. `ApprovalChainsController.cs:15` is `AdminOnly` = `RequireRole(Roles.Admin)` (`Program.cs:70`) →
   `GET` 403 → `chainsError = "Request failed (403)"`.
3. The **New chain** button renders unconditionally (`:118-120`). The user fills the form, clicks Create.
4. `POST` 403 → `setFormError(...)` at `:101` → **masked** at `:55`. Both strings are identical
   (`Request failed (403)`), so the banner text does not even change. `busy` flips back, form stays open.

**The user's click produces zero visible change.** This is D2 — an action whose failure is invisible —
reinstated through a different door, and it is a **regression**: before the diff, `submit()`'s catch
wrote to the `error` rendered *inside the form Card*, so the POST 403 was shown.

Breaks CLAUDE.md's "match existing patterns rather than inventing one". The doc comment at `:42-45`
claims each state "must not be clobbered or wiped by an unrelated action" — true of the variables,
false of what the user sees.

No test covers `chainsError` set + POST rejected, which is why the suite is green over it. The
nearest test (`challenger_m1_2.test.tsx:317-335`) serves `chains: []` — a *successful* load — so the
mask never engages.

## Confirmed genuinely fixed

**D1 is structurally fixed on every path constructed**: successful `[]` → truthful empty state;
`204`/empty body → `data ?? []` at `:60`; `Error` rejection → `chains` stays `null` so
`chains?.length === 0` is false; non-`Error` rejection → canned fallback; `message === ''` →
`.trim()` guard at `:19` prevents the falsy-`''` alert drop. `:212`'s `!chainsError` correctly
prevents a permanent "Loading…". The literal D2 path (New chain → Cancel) is fixed at `:203`.

**The test rewrites are not tautologies** — each verified by reverting its fix:
`challenger_m1_2.test.tsx:98-108` fails if `errorMessage()` reverts; `:145-161` fails if the
`usersError` option reverts, and asserts `toBeDisabled()` so a fake approver won't satisfy it;
`challenger_m1_1.test.tsx:214-231` fails if `approverLabel` reverts. Not refusal-only —
`challenger_m1_1.test.tsx:251-284` drives a full create and asserts the POST body.

**The Orchestrator's ruling on the contradictory test was correct** — the original assertion was the
literal negation of `:81` in the same file; both could not pass.

## 🟡 findings

2. **`?? []` applied to one of three sibling loads.** `:60` normalises an empty body; `:62`
   (departments) and `:64` (users) do not. `users === undefined` would reach `users.find` at `:30`
   → the exact bug M1 exists to fix. Not 🔴 only because both endpoints always emit a body today.
   **The rule applied to one sibling and not the other two** — this repo's signature defect.
3. **D4's explanation applied to the approver select but not the department select.** Worse case:
   the approver select is `required` so a missing approver blocks the POST, but an unselected
   department is a *valid* submission meaning company-wide. A user whose `/departments` load failed
   can create a chain they believe is department-scoped and get a **company-wide** one, applying to
   every requisition via the fallback (`RequisitionService.cs:152-155`).
4. **D3 is half-closed and `worker_m1_fix`'s handoff overstates it.** `auxError` still has two
   writers (`:63`, `:67`) with last-write-wins — D3's actual complaint.
5. **`chainsError` + successful POST renders an incoherent screen**: a list containing only the
   just-created chain, under a permanent "Could not load approval chains." banner.
6. **The blank-`displayName` rule applied to the label but not the picker** — `:183-185` renders
   `{u.displayName} ({u.role})` raw, so a whitespace name shows as `" (Recruiter)"`.
7. **Weakest pin in the file**: `challenger_m1_2.test.tsx:227-230` accepts `queryByText('Loading…')`
   as a pass; it pins `data ?? []` only because RTL's `act()` happens to flush first.
8. 🟢 Unexplained non-null assertion at `challenger_m1_2.test.tsx:200`.

No `any` anywhere in the diff; `unknown` narrowed properly at `:18`.

---

## Orchestrator note — this triggered the remediation cap

Gate 1 REJECT → loop 1 (`worker_m1_fix`) → loop 2 (`worker_m1_fix2`) → re-gate REJECT.
That is **two remediation loops spent and a third failure**, which the command defines as the point
to stop and bring the milestone to the user rather than dispatch a fourth agent.

The signal the cap exists to catch is present and real: **loop 1's own fix introduced this
regression** by removing the in-form error render while collapsing three states into one slot. The
milestone is not flailing at random — each loop found genuine defects — but remediation is now
introducing defects as fast as it closes them, on a page whose error-state design was never
specified up front.
