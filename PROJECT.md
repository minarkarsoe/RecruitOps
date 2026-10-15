# PROJECT — Approval chain flow: why it is unfinished, and the plan to finish it

Teamwork run `tw2`. Supersedes the `tw1` plan (M1–M8), which remains recorded in
`.agents/tw1/orchestrator/`. Branch `develop`.

## The question that started this

> "I've been looking at the approval chain flow and it's still not finished — check why."

## The answer

**The approval engine was never the problem.** It shipped in `b497faf` (2026-07-28), is not
stubbed, and its backend suite passes 17/17 when actually run. Submit stamps out approval rows
from the chain template, sequential turn order is enforced, status transitions are correct, and
the Inbox queries real department-scoped data.

Three things stand between the user and that working engine:

1. **The chain admin screen crashes.** `GET /api/users` changed shape on 2026-07-30 (`85c3e39`)
   and its one earlier consumer was never updated. Blank page, no error.
2. **The permissions that would make approval configurable are dead.** Seeded, granted,
   displayed in the Role Builder — and never checked by any approval endpoint.
3. **The canonical spec says the feature does not exist.** `01-job-requisition-approval.md:3`
   still reads "Status: ⬜ Not started".

Any one of these alone is enough to make the feature feel permanently unfinished.

## The user's design decision, and how it was interpreted

Asked who should manage approval chains, the user answered:

> "We can config who can do the approval chain base on who request or which department. Eg,
> I'm hiring manager, I can approve which department need to request for hiring new employees.
> You can also read the doc first if you not sure."

This matched none of the three options offered (Admin-only / HrDirector-too / Admin-plus-picker-fix).
Reading the doc as instructed, module spec **1.3 Dynamic Approval Workflow** (`:20-23`) says the
chain routes by the company's own org structure and is *"configurable per company, not
hard-coded."*

**Interpretation adopted, stated explicitly so it can be corrected:** approval authority is to
be **permission-driven and department-aware**, not pinned to a hardcoded role literal. Concretely
— stop gating approval endpoints on `RequireRole(Roles.Admin)` and start gating them on the
already-seeded `permission:requisitions:requisitions:*` codes, so the Role Builder genuinely
controls who approves, while the existing per-department chain resolution
(`RequisitionService.cs:152-155`, department chain first, company-wide fallback) supplies the
"which department" half.

**Risk of misreading:** the example sentence could instead mean a *new* capability — a hiring
manager self-selecting which departments they approve for. The existing model already assigns
named approvers per department chain, so that capability is largely present; if the user meant
something beyond it, M2's contract is the place to correct course, before the Worker builds.

## Milestones

One Worker owns one milestone. Milestones run **sequentially, not in parallel** — every Worker
runs the suites against one shared working tree, so a concurrent Worker's half-finished edit
lands in another's reported numbers. A false green is the exact failure this structure exists to
prevent.

| # | Milestone | Why | Depends on | Status |
|---|---|---|---|---|
| **M1** | Fix the `GET /users` contract crash on `ApprovalChainsPage` | 🔴 Nothing about chain admin is usable until this is fixed. Repointed at `GET /users/selectable` (bare array, ADR-0019 picker pattern) rather than unwrapping `.items` — three agents independently confirmed `.items` would have silently truncated the picker at `pageSize=20`. | — | **STOPPED AT THE REMEDIATION CAP — see below** |
| **M2** | Permission-driven, department-aware approval authority | 🔴 The user's actual request. Migrate `RequisitionsController` + `ApprovalChainsController` off `RequireRole` literals onto `[HasPermission(...)]` so the seeded codes stop being decorative. **Touches authorization — `security-reviewer` required before this is called done (CLAUDE.md).** Needs an ADR. | M1 | DONE (ADR-0022) |
| **M2.5** | **Revise-and-resubmit a rejected requisition** | 🔴 User-requested 2026-08-14. Today `Rejected` is terminal and `UpdateAsync` permits edits only in `Draft`, so a rejection can never be fixed — a new requisition must be raised, losing the thread. **Changes a documented rule**, so it needs an ADR + module-doc update *before* code, and a schema change for round history (see contract below). Shares `RequisitionService`/`RequisitionsController` with M2, hence the ordering. **Delivered together with the senior skip-ahead rule (M2.6) — one migration, one decision path.** | M2 | DONE (ADR-0023) |
| **M2.6** | **Senior approver may skip ahead** | 🔴 User-requested 2026-08-15: *"I am number 2 and you are number 1 — I can skip over you and approve both 1 and 2, but it must show the record of what I did."* A later step outranks an earlier one and may approve everything at or below it; reject stays bound to the caller own step. **Reverses a deliberate decision** (`GetInboxAsync` existed to prevent exactly this) and **touches authorization — `security-reviewer` required**. Shares the migration and `DecideAsync` with M2.5. | M2.5 | DONE (ADR-0024) |
| **M3** | Chain template edit + deactivate | 🟡 `PUT`/`DELETE` absent on both sides; a typo in a chain is currently permanent. New endpoints must carry M2's policy, hence the ordering. | M2 | NOT STARTED |
| **M4** | ErrorBoundary + page-level tests for the routed approval pages | 🔴 `pages/RequisitionsPage`, `RequisitionDetailPage`, `ApprovalChainsPage`, `InboxPage` have **zero** tests, and there is no ErrorBoundary anywhere in `frontend/internal/src` — which is why M1's bug rendered as a blank page. Follow `InterviewDetailPage.test.tsx`'s `vi.mock('../lib/api')` pattern. | M1, M3 | NOT STARTED |
| **M5** | Resolve the orphaned `features/requisitions/*` tree | 🟡 Zero importers repo-wide, yet 6 test files / 69+ cases exercise it — inflating the green count while testing nothing a user sees. Delete it, or wire it in and retire `pages/*`. **Product decision — surface to the user, do not let a Worker pick silently.** | — | NOT STARTED |
| **M5.1** | Fix `BulkCvUploadModal` shape drift | 🔴 **Found by `reviewer_m1_2` during the M1 gate, same bug class as M1.** `BulkCvUploadModal.tsx:254` reads `batchStatus.files` / `processedCount`; the backend `BulkBatchStatusDto` (`BulkResumeDtos.cs:28-40`) sends **`items`** / **`processedFiles`**, unmapped (`JobPostingsController.cs:178-192`). A recruiter uploading CVs gets `undefined.map` — the modal blanks mid-upload — and `:245` computes `width: 'NaN%'`. Green today because both its test files mock the **frontend** shape. | — | NOT STARTED |
| **M5.2** | Implement `PUT /api/applications/{id}/profile` | 🔴 **Confirmed by `reviewer_m1_2`** after being UNVERIFIED twice. `lib/api.ts:189` calls it; `ApplicationsController` has only `POST {id}/stage`, `GET {id}/history`, `POST {id}/resume`, `GET {id}/resume`. **ADR-0008's mandatory human-confirmation gate has never worked** — it 404s. Three test files mock the call and assert it fired, so the mock is the only thing answering. | — | NOT STARTED |
| **M5.3** | Let the `Interviewer` role reach interviews and scorecards | 🔴 **Found 2026-08-15 while seeding demo data — an empirical find, not a code read.** `RbacSeedData.cs:158-165` grants `Interviewer` exactly `interviews:read`, `scorecards:read` and `scorecards:submit`. But `InterviewsController` is gated on `Policies.InternalUser` = `RequireRole(Admin, HrDirector, Recruiter, HiringManager, Approver)` (`Program.cs:66-68`) — **`Interviewer` is absent**, so every interview and scorecard endpoint 403s for the one role that exists to use them. An interviewer cannot see their own interview or fill in a scorecard. Same defect class as M2: a role-literal gate and the permission system disagreeing, and the permission system losing. **Touches authorization — `security-reviewer` required.** | M2 | NOT STARTED |
| **M6** | Docs reconciliation | Module doc still says "⬜ Not started"; `FEATURE-STATUS.md`/`NEXT-SESSION.md` carry test counts ≥5 commits stale. CLAUDE.md makes this part of the change, not an afterthought. | M1–M5 | NOT STARTED |

## Scale, stated plainly

Six milestones through the full gate is ~36 agent dispatches (Worker + 2 Reviewers +
2 Challengers + Auditor each), on top of the 3 Explorers already spent. This will very likely
outlive this session. The user chose "everything found" with that cost disclosed. If the run
has to stop mid-way, `PROJECT.md` and `.agents/tw2/orchestrator/progress.md` are written so the
next session resumes at the milestone boundary rather than re-deriving the survey.

## Deliberately out of scope

- ~~Revise-and-resubmit for rejected requisitions.~~ **Moved into scope as M2.5** — the user
  confirmed on 2026-08-14 that this is wanted. Contract below.
- Parallel approval steps, approver delegation, escalation timeouts, auto-creating the posting on
  approval, budget/headcount draw-down — all listed as open questions in the module spec `:57-65`
  and explicitly not part of "finished" for this module.
- The app-wide `[HasPermission]` migration beyond Modules 1's controllers.

## M1 — stopped at the remediation cap, and exactly where it stands

**Status: code green, milestone NOT PASSED.** Suite **[ORCH-VERIFIED]** at 42 files / 347 tests
passing, typecheck clean. But the gate never reached 4×APPROVE + CLEAN, and a verdict is not
re-interpreted because the code later improved.

Sequence: gate 1 → 3 APPROVE / 1 REJECT · loop 1 (`worker_m1_fix`) · loop 2 (`worker_m1_fix2`) ·
re-gate → REJECT. **Two remediation loops spent, third failure — the command says stop here and
bring it to the user rather than dispatch a fourth agent.**

### What M1 genuinely fixed (all verified)

The original crash; the false "No approval chains yet" over a failed load (fixed *structurally* —
`chains` stays `null`, so the empty-state gate cannot fire over an error); Cancel wiping a load
error; a departed approver rendering as a raw GUID; a whitespace-only name blanking the row; an
empty `ProblemDetails.detail` silencing the alert (in shared `lib/api.ts`, affects every page); and
an unfillable approver dropdown that explained nothing.

### The one open 🔴

`ApprovalChainsPage.tsx:55` — `const error = chainsError ?? formError ?? auxError`. `chainsError`
is never cleared and `formError` has no independent render site, while the diff **removed** the
in-form error line. So once the chain load 403s, a failed *Create chain* click produces **zero
visible change** — D2 reinstated through a different door, and a regression introduced by loop 1.

**The fix is small and already has an in-repo precedent:** `RolesPage.tsx:13` + `:306-310` keeps a
page-level `error` and a separate `formError` rendered inside the form, never collapsed. Restore the
in-form render rather than deepening the precedence chain.

### Why the cap fired rather than one more loop

Each loop found real defects — this was not flailing. But **loop 1's own fix introduced the
regression loop 3 caught**, on a page whose error-state design was never specified up front. That is
the signal the cap exists to catch: the plan lacked a decision it needed, so remediation is
introducing defects about as fast as it closes them. The missing decision is *"what is this page's
error model — how many error owners are there, and where does each render?"* Answer that first,
then one Worker closes it.

### Also still open on this file (🟡, from the re-gate)

`?? []` normalisation applied to the chains load but **not** to the departments or users loads (the
repo's signature sibling-drift defect, and the same shape as the original bug); D4's inline
explanation applied to the approver select but not the department select — where the failure is
worse, because an unselected department is a *valid* submission meaning company-wide, so a user can
create a company-wide chain believing it is department-scoped; `auxError` still has two writers with
last-write-wins; and a successful POST under a `chainsError` renders a one-item list beneath a
"Could not load approval chains." banner.

## M2.5 contract — revise-and-resubmit (agreed before the Worker starts)

The user asked for this directly: *"Reject ဖြစ်ရင် ပြန်ပြင်ပြီး resubmit လုပ်လို့ရအောင်လုပ်ပေးပါ"*
— if it is rejected, let it be fixed and resubmitted.

**This contradicts a documented rule.** Module doc `:42` states `Rejected` is terminal.
CLAUDE.md: *"Spec change → update the module doc **before** the code"* and *"Hard-to-reverse
decision → write an ADR"*. Both are part of M2.5, not follow-up.

### Decisions taken from the spec's own philosophy, not invented

1. **Rejection history is never overwritten.** The module doc's cancellation section (`:51-53`)
   already rules on this exact question: *"Cancelling does not touch the approval steps. A chain
   left half-decided is the honest record of what happened; rewriting the steps would fabricate
   decisions nobody made."* A resubmit therefore opens a **new round**; the prior round's rows —
   including who rejected it and why — remain readable forever.
2. **Resubmission restarts the chain at step 1.** If the requester edits the salary budget after
   Finance rejected it, an earlier `Approved` was granted to a *different document*. Carrying it
   forward would credit approvals nobody gave to the version now in flight. Every round is
   decided afresh.
3. **Only the requester may revise**, mirroring cancellation (`:46-47` — withdrawn by the person
   who raised it). An approver's tool remains Reject.
4. **`Approved` and `Cancelled` stay terminal.** Only `Rejected` gains an exit. Approved work
   must not be reopened silently, and a withdrawn request is withdrawn.

### Shape

- `RequisitionApproval` gains a **round/attempt** discriminator so rounds are distinguishable.
  → **EF migration required.** Per CLAUDE.md the Worker **proposes** it; it is **not applied**
  here, and it must go to the `db-schema-reviewer` subagent.
- A transition out of `Rejected` back to an editable state, requester-only, guarded so
  `Approved`/`Cancelled` cannot use it.
- Reads that show "the approval trail" must show the **current** round without hiding earlier
  ones — the Inbox and `awaiting` label must key off the live round only, or approvers will see
  stale rows. `RequisitionService.cs:46-91` (inbox) and `:356-372` (detail projection) both
  assume one round today and must be re-read, not patched blind.

### Resolved — revised requisitions return to `Draft`

**User decision, 2026-08-14: back to `Draft`.** No new `RequisitionStatus` value is introduced,
so no consumer (badges, filters, Inbox, analytics, `packages/types`) has to learn one. The
existing rules carry the weight: `Draft` is already editable, submit-from-`Draft` already stamps
out approval rows, and post-submit freezing (409) already applies.

Resulting transition: `Rejected --(requester revises)--> Draft --(submit)--> PendingApproval`,
with the submit opening round *n+1* and leaving round *n*'s rejection intact.

**Consequence the Worker must handle:** `Draft` currently implies "never submitted". After this
change it no longer does. Any code that infers "this was never submitted" from `Status == Draft`
is now wrong — the round history, not the status, is the source of truth for that. The Worker
must grep for that assumption rather than assume it does not exist.

## Known gaps recorded but not fixed here

- `apiFetch<T>`'s unchecked `as T` cast (`lib/api.ts:103`) lets any frontend/backend shape drift
  pass `npm run typecheck`. M1 fixes one instance; the systemic hole remains.
- Backend tests run on EF's in-memory provider (`CustomWebAppFactory.cs:106`), so no approval
  query has ever been proven against real Postgres.
- `DepartmentsPage` has the identical nav-gate/API-gate mismatch as `ApprovalChainsPage`
  (`Sidebar.tsx:87-90` vs `DepartmentsController.cs:32-34`).
