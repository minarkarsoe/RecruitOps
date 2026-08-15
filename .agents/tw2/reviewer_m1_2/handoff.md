# reviewer_m1_2 — M1 review, remit: architecture, pattern consistency, test adequacy

Filed by the Orchestrator from the reviewer's text reply.

## VERDICT: APPROVE — 0 🔴

## Worker's numbers independently verified — exact, no inflation

Measured on the Worker's tree *before* either Challenger file existed:
- `npm run typecheck` (root) — **clean**, both workspaces
- `npm run test --workspace @recruitops/internal` — **40 files / 320 tests passed**, 9.79s

## ⚠️ Tree state warning — the suite is currently RED, and it is not M1's fault

As of this review: **41 passed / 1 failed (42 files), 337 passed / 9 failed (346 tests)**. Every
failure is in the untracked `ApprovalChainsPage.challenger_m1_2.test.tsx`.

**Diagnosis:** `frontend/internal/vitest.config.ts:16` sets no `globals`, so RTL's auto-`cleanup`
is never registered. That file calls `render()` repeatedly without cleanup, so `findByRole('alert')`
sees stacked DOM copies. Two of its tests **pass when run in isolation** (`-t`) and fail only in a
whole-file run. **A defect in the Challenger's test file, not in `ApprovalChainsPage`.**
**Do not merge the tree red.**

The reviewer also witnessed the other Challenger's mutation window (file reverted to HEAD at
22:49:48, restored 22:51:18) and used it as an unplanned red/green pin: with the fix reverted all
6 of `challenger_m1_1`'s tests fail on the real `TypeError`; restored, all 6 pass.

## Item 1 — is repointing the right fix? **Yes.**

The page renders exactly two fields per user: `:144` (`{u.displayName} ({u.role})`) and `:215`
(`users.find(...)?.displayName`). `SelectableUserDto` (`UsersController.cs:44-47`) carries `Id`,
`DisplayName`, `Role` — everything rendered, nothing missing.

Third independent confirmation that `.items` would have been **strictly worse**: the picker would
have silently held only the first page of the directory — *"a worse failure than the crash, because
it looks like it worked."*

## Item 4 — error handling matches the house pattern. **Yes.**

`InboxPage.tsx:9,14,26` is the near-exact sibling — one `error: string | null`, `.catch` setting
it, a `role="alert"` `<p>` above the content. `DepartmentsPage.tsx:18,34,41-46,57` likewise.

## Item 3 — the deferral to M4 was the wrong call, but the gap is closed in-tree

Shipping a fix for a *runtime crash* with no regression test is this repo's documented failure
mode, and "M4 owns page tests" does not cover it: **M4 depends on M1 and M3**, so the pin would
have sat unwritten across two more milestones on a page that has already crashed in production.
The counter-argument (duplicating M4's template invites churn) fails because what is needed is a
*contract* test, not a coverage test, and it costs ~40 lines.

Not blocking **only because** `challenger_m1_1`'s file now exists and does the right thing — its
mock serves both real shapes, so repointing back at `/users` goes red on the actual `TypeError`.

> **Binding condition from the reviewer:** that file is **untracked**. If it is discarded when M1
> is committed, this finding becomes a 🔴 with no test behind it. **Commit it with the fix.**

## 🔴-class findings outside M1's scope — the highest-value output of this review

### 🟡 1 — The identical `as T` crash is still live in `BulkCvUploadModal`
`features/pipeline/BulkCvUploadModal.tsx:254` — `batchStatus?.files.map(...)`.
`BulkResumeBatchStatus` (`packages/types/src/index.ts:887-899`) declares `files` and
`processedCount`; the backend record `BulkBatchStatusDto` (`BulkResumeDtos.cs:28-40`) has
**`Items`** and **`ProcessedFiles`**, returned unmapped by `JobPostingsController.cs:178-192`.

Concrete: recruiter uploads 3 CVs; first poll resolves; `files` is `undefined`; the optional chain
guards only `batchStatus`, so `undefined.map` throws and **the modal blanks out mid-upload**.
Two lines earlier `:245` computes `Math.round((undefined / 3) * 100)` → `NaN` → `width: 'NaN%'`.
Also `fileItem.candidateName` (`:258`) never renders — no such field on the DTO.

Green today because both its test files mock `resumeApi` with the **frontend** shape. A green suite
over a broken feature.

### 🟡 2 — `PUT /api/applications/{id}/profile` does not exist
`lib/api.ts:189` issues it; `ApplicationsController` has exactly four actions — `POST {id}/stage`,
`GET {id}/history`, `POST {id}/resume`, `GET {id}/resume`. `Program.cs:207` is `app.MapControllers()`
with no minimal-API routes.

Concrete: a recruiter edits parsed CV fields in `CandidateSlideOver.tsx:153` and confirms → **404**.
**The explicit-human-confirmation step of the CV pipeline has never worked** (ADR-0008's mandatory
gate). Three test files mock it and assert it was called — the mock is the only thing answering.

> This **confirms** the item left UNVERIFIED by `explorer_ac_2` and by the earlier `tw1` run.

**The reviewer checked every other `api<...>` call site against its controller signature** —
analytics, search, resume extraction, roles/permissions, departments, requisitions, interviews,
scorecards, notes, pipeline and `/version` all match field-for-field. **These two are the only
remaining drifts.**

### 🟡 3 — A failed chain load still renders "No approval chains yet"
Third independent report of the same defect (`:28` vs `:173-182`). Adds two consequences the others
did not: for an Admin hitting a 500 it **invites creating a duplicate chain**, and
`ApprovalChainService.CreateAsync` has **no duplicate guard** while `RequisitionService` resolves
department-chain-first — so a duplicate **silently changes approval routing**.
`InboxPage.tsx:27` is the sibling that gets this right: leaves `items` null, guards the empty state
on `!error`.

### 🟡 4 — Cancel wipes a *load* error
`:163`. The banner is page-level (`:86`) but the clear is form-scoped. After a 403 on load,
"New chain" → "Cancel" makes the screen **byte-identical to the pre-fix silent failure this
milestone existed to remove**.

### 🟡 5 — Empty `ProblemDetails.detail` produces no alert at all
`lib/api.ts:159` — `problem?.detail ?? problem?.title ?? …`. `??` falls through only on
null/undefined, so `{"detail": ""}` yields `''`, and `{error && <p role="alert">}` renders nothing
because `''` is falsy. **The user gets the pre-fix experience: no error, empty state.** Affects
every page using this idiom. `readError` should treat empty/whitespace as absent.

### 🟢 6 — Three concurrent loads, one `error` slot
`:27-32` race; last settled wins. Is the house pattern, but siblings have one writer and this has three.

### 🟢 7 — Deactivated approver renders as a raw GUID
Fourth report. M3 should not land without addressing it.

### 🟢 8 — `any` in a shared type
`packages/types/src/analytics.ts:67` — `Record<string, any>[]`. CLAUDE.md forbids `any`.
Pre-existing, unrelated to M1.
