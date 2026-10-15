# explorer_ac_3 — tests, docs and project history for the approval chain

Filed by the Orchestrator from the explorer's text reply (subagents cannot write files here).
Claims marked **[ORCH-VERIFIED]** were re-checked independently by the Orchestrator.

## Summary

The approval chain **was** claimed finished, repeatedly, in the two status docs that matter
most — and on the backend that claim is honest: the test suite genuinely drives the full
lifecycle. The real problems are (a) the module spec still says "Not started", and (b) the
frontend has **two parallel requisition UIs**, and the one carrying all the test coverage is
the one the app never renders.

## What the spec says "finished" means

From `docs/product/modules/01-job-requisition-approval.md`:

1. Hiring Manager submits a requisition (title, salary budget, requirements) — 1.1, `:12-14`
2. JD Template Library — pull a pre-saved JD master template — 1.2, `:16-18`
3. Approval chain **configurable per company** (Dept Head → Finance → HR), not hard-coded — 1.3, `:20-23`
4. Requester can see **whose desk the request is on right now** — 1.4, `:25-27`
5. Status vocabulary `Draft → PendingApproval → Approved | Rejected`, plus `Cancelled`, with per-step `Waiting/Approved/Rejected` — `:37-40`
6. Cancellation: requester or company-wide role only, from `Draft`/`PendingApproval` only, approval steps left `Waiting` — `:44-54`
7. Draft editable before submission, frozen after (409) — `:60-62`
8. **Explicitly out of scope for "finished"**: parallel approval steps, delegation/escalation timeout, auto-creating the posting on approval, budget/headcount enforcement — `:57-65`

## Docs vs reality

| Claim | Source | Actual state |
|---|---|---|
| "✅ API + UI · full loop drivable from the browser" | `FEATURE-STATUS.md:30` | **[ORCH-VERIFIED]** Backend true. Frontend: the routed page implements it and calls the real endpoint, but has **no test file**. |
| "Module 1 — ✅ API + UI + tests, end to end" | `NEXT-SESSION.md:71` | Backend tests substantive. "tests" for the UI half is misleading — see orphan finding. |
| "**Status:** ⬜ Not started" | `docs/product/modules/01-job-requisition-approval.md:3` | **[ORCH-VERIFIED]** Stale. Written 2026-07-28 (`44fa4e8`) *before* the feature, never synced after it shipped the same day in `b497faf`. Directly contradicts `FEATURE-STATUS.md:30`. **CLAUDE.md calls the module docs the single source of truth — so the canonical doc says this feature does not exist.** |
| Approval code actively maintained | implied | Every backend approval file touched in exactly **one** commit (`b497faf`, 2026-07-28) and never again. `ApprovalChainsPage.tsx` likewise (`24e3c9c`). Shipped once, left alone. |
| "~190 uncommitted files" (dispatch assumption) | task brief | **Not true for this area.** `git status --porcelain` shows 1 modified file repo-wide, unrelated. Approval work is entirely in history. |
| An ADR governs approvals | — | None exists. ADR-0003 (dept scoping) and ADR-0018 (approver candidate-data exclusion) constrain *who*, not workflow. The stale module doc is the only spec. |
| "318/318 Vitest, 39 files" | `NEXT-SESSION.md:37` | Stale by ≥5 commits. Real count unverified — Orchestrator must re-run, not trust it. |

## Backend test coverage — genuinely good

`backend/tests/RecruitOps.Api.Tests/RequisitionApprovalFlowTests.cs`, 391 lines:

| Test | Catches a missing instantiation step? |
|---|---|
| `Full_Chain_Approval_Moves_Through_Both_Steps` `:42-80` — asserts `Approvals.Count == 2`, all `Waiting`, immediately after submit | **YES** — fails at `:57` if submit didn't snapshot the chain |
| `Rejection_At_First_Step_Rejects_The_Requisition` `:83-100` | YES (sequencing half) |
| `A_Later_Approver_Cannot_Jump_The_Queue` `:103-115` | Partial |
| `Inbox_Only_Shows_Requisitions_Waiting_On_You` `:320-347` | YES-adjacent |
| `Submitting_Twice_Is_A_Conflict`, ownership/edit/cancel/leak tests `:118-317` | NO — CRUD/auth-adjacent |
| `Admin_Can_Create_A_Chain_But_Recruiter_Cannot`, `Chain_With_An_Unknown_Approver_Is_Rejected` `:350-390` | NO — CRUD, but coexists with lifecycle tests rather than standing alone |

**Verdict: this is NOT the green-suite-over-broken-feature pattern.** The suite drives
create → submit → approve → approve → terminal, and reject, and cancel.

**Caveat:** `CustomWebAppFactory.cs:106` uses `UseInMemoryDatabase`, not Postgres.
`NEXT-SESSION.md:141-142` documents this trap class (EF Core 10 won't translate
`enum.ToString()` or correlated subqueries in a `Select`). Nobody has checked whether
`ApprovalChainService`/`RequisitionService` carry that pattern — green in-memory does not
rule it out.

## 🔴 The load-bearing frontend finding — two UIs, tests on the wrong one

**[ORCH-VERIFIED]** `grep -rn "features/requisitions"` across `frontend/` and `packages/`
returns **no imports at all**. `App.tsx:6-13` routes exclusively to `pages/*`.

| Tree | Routed? | Tests |
|---|---|---|
| `pages/RequisitionsPage.tsx`, `RequisitionDetailPage.tsx`, `RequisitionFormPage.tsx`, `ApprovalChainsPage.tsx`, `InboxPage.tsx` | **YES** — `App.tsx:29-54` | **ZERO** — only `InterviewDetailPage`, `RolesPage`, `UsersPage` have tests under `pages/` |
| `features/requisitions/{RequisitionTable,RequisitionDrawer,useRequisitions,index.ts}` | **NO — zero importers** | **SIX files, 69+ cases** |

The six: `features/requisitions/requisitions.test.tsx`, `milestone3EmpiricalChallenge.test.tsx:389-438`,
`challenger_m3_retry_2.test.tsx`, `challengerEmpiricalStress.test.tsx:436,462`, plus others.

**Consequence:** if someone broke the `decide()` call in `RequisitionDetailPage.tsx:57-64` —
wrong URL, wrong verb, dropped `comment`, swallowed error — **no frontend test would fail.**
Worse, `milestone3EmpiricalChallenge.test.tsx` asserts an "Approve Requisition" button
`.toBeInTheDocument()` without ever clicking it or asserting `onDecide` fires.

**Origin:** the orphan tree came from `a2cdac0` ("feat(ui): add design-system primitives, app
shell and feature modules") — a separate multi-agent track that rebuilt the requisitions
screen as an "app shell" and never wired it into `App.tsx`. No commit or doc anywhere records
this as a deliberate pause. It is an unreconciled artifact of two parallel agent tracks.

## Traps for a Worker

- `NEXT-SESSION.md:176`: "Adding a rule to two of three sibling methods is the recurring bug
  in this repo." This is the same shape one level up — two whole component trees.
- `NEXT-SESSION.md:85-100` backlog item 1 ("Frontend tests for Modules 1–2: none") is still
  true for the *live* UI. **Do not let it be closed by pointing at the orphan tests.** New
  tests must target `pages/*`, following `InterviewDetailPage.test.tsx`'s `vi.mock('../lib/api')` pattern.
- Deleting vs. wiring up the orphan tree is a product/architecture call, not a docs fix.

## Open Questions

1. Was the user's complaint based on the **running app** or on **reading the module doc**? If
   the latter, `01-job-requisition-approval.md:3` saying "⬜ Not started" is very plausibly
   the entire explanation — a one-line fix.
2. Delete the `features/requisitions` tree and its four test files, or finish wiring it up and
   retire `pages/*`? No doc, ADR or commit states an intent either way.
3. Reconcile the stale test counts in this pass, or leave to whoever next runs the suite?
4. Does the Orchestrator want a real-Postgres check of the approval queries before calling the
   backend closed?
