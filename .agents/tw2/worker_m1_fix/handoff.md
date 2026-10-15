# worker_m1_fix — M1 remediation, loop 1 of 2

Filed by the Orchestrator from the Worker's text reply. **[ORCH-VERIFIED]** = re-checked directly.

## What was fixed

D1, D2, D5, D6, D8 — in `ApprovalChainsPage.tsx` and the shared `lib/api.ts`.

### D1 / D2 shape

Followed `InboxPage.tsx` exactly rather than inventing a variant:

- **`chains` is never set to `[]` in the catch.** It stays `null` on a failed load, so
  `chains?.length === 0` — the sole gate on the empty-state card — is *structurally* false whenever
  the load failed. No extra `&& !error` guard was needed, same as the sibling.
- One `error` state was **split three ways**: `chainsError` / `auxError` (departments+users) /
  `formError`, with a computed `error = chainsError ?? formError ?? auxError` (`:46-51`).
- `submit()` (`:80-97`) and Cancel (`:192`) now touch **only `formError`**, so a load error survives
  New chain → Cancel. The Worker's reasoning: the two are semantically different actions with
  different owners (page mount vs. form lifecycle), and the bug was precisely that a form action
  cleared state it did not create.
- **D3 closed as a free byproduct** of that split — a departments/users failure can no longer
  clobber a chains failure. Not requested; fell out of the architecture.
- `approverLabel()` helper (`:22-34`): "Unknown approver (no longer active)" when the user is
  absent from `/users/selectable` (**D5**); falls back to the raw id rather than blank when a
  matched user's `displayName` is whitespace-only (**D6**).
- `errorMessage()` helper (`:17-20`): trusts `e.message` only when non-blank.
- `lib/api.ts` `readError()` (`:156-169`): treats blank/whitespace `detail`/`title` as absent
  instead of `??`, which only falls through on null/undefined (**D8**). Every caller checked;
  full suite re-run to confirm no other page regressed.

## The red-tree disagreement — settled empirically, and **`reviewer_m1_2` was wrong**

**[ORCH-VERIFIED]** `frontend/internal/src/test/setup.ts` — a **pre-existing, unmodified** file
(commit `5b6538e`, predates this run) — already does:

```ts
afterEach(() => { cleanup(); sessionStorage.clear(); });
```

Its own comment explains the exact hazard the reviewer alleged. `setupFiles` execution is
independent of the `globals` config flag — `globals` controls only whether `describe/it/expect` are
ambient. **RTL cleanup was already correctly wired**, so the reviewer's diagnosis does not hold on
this codebase.

The Worker then proved the pins were genuine: with the two product files reverted (`git stash`,
tracked only — the untracked test files stayed), `ApprovalChainsPage.challenger_m1_2.test.tsx` ran
**17 of 18 failing** against pre-fix code. **`challenger_m1_2` was right; `reviewer_m1_2` was not.**

## Two tests left red, deliberately, and reported rather than edited

**[ORCH-VERIFIED]** Suite at handoff: **345 passed / 2 failed (347)**. Typecheck clean.

1. **`'DEFECT: an ApiError carrying an empty detail renders no alert at all'` (`:98-107`)** —
   serves `chains: forbidden('')` and asserts `await screen.findByText(EMPTY_STATE)` **succeeds**.
   That is the literal bug D1 exists to remove, and it contradicts the test a few lines above in the
   same file. The Worker followed the D1 requirement over this one assertion and **flagged it for
   adjudication instead of silently editing it**.
   → **Orchestrator ruling: the test is wrong.** The Challenger used "empty-state card present" as a
   proxy for "page mounted"; D1 invalidated the proxy. Its *intent* (D8) is valid and must survive.
   Sent to `worker_m1_fix2` to rewrite the assertion, not revert the fix.
2. **D4 (`:156`)** — the create form opens with a one-option approver dropdown after
   `/users/selectable` fails, and the form says nothing about why. Not on the previous dispatch's
   required list; fixing it needs the same null-vs-empty distinction applied to `users`.
   → **Orchestrator ruling: in scope now.** Same silent-failure disease the 🔴s were rejected over.
   Sent to `worker_m1_fix2`.

**D9 now passes** as another byproduct — `setChains(data ?? [])` normalises an empty success body
into a real empty list.

## One test modified — strengthened, not weakened

`ApprovalChainsPage.challenger_m1_1.test.tsx`'s `KNOWN GAP` test carried a comment instructing
"flip the two expectations when the display lookup stops depending on the picker list" — exactly
what D5 does. The Worker flipped it: it now asserts the friendly fallback **and** the absence of the
raw GUID — strictly more assertions than before — and renamed it off the `KNOWN GAP:` prefix. The
other 6 tests in that file (the crash regression pin) are untouched and green.

## Sibling defect found by grepping, not fixed

`JdTemplatesPage.tsx:106` has the identical `onClick={() => { setShowForm(false); setError(null); }}`
shape — a load error there is also wiped by Cancel. Out of M1's dispatched file scope; flagged
rather than fixed silently. **Recorded for a later milestone.**

## Commands, real output

```
npm run typecheck   → clean, both workspaces
npx vitest run      → Test Files 1 failed | 41 passed (42)
                      Tests      2 failed | 345 passed (347)
```

Backend untouched (no `.cs` in the diff), so `dotnet` suites were correctly not run.
