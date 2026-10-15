# challenger_m1_2 — M1 empirical challenge, remit: failure modes, inputs, defaults

Filed by the Orchestrator from the challenger's text reply.

**Deliverable:** `frontend/internal/src/pages/ApprovalChainsPage.challenger_m1_2.test.tsx`
— 18 tests: 9 pass, **9 left red on purpose** as defect pins. Product code untouched
(`git diff --stat` restored byte-identical after every mutation).

## VERDICT: REJECT — 2 🔴

Baseline confirmed independently: **40 files / 320 tests** — the Worker's number is accurate.
`npm run typecheck` clean.

Also established: `grep -rln "ApprovalChainsPage"` returns only `App.tsx` and the page itself —
**the M1 change shipped with zero tests.** There was no test to break, so the "break the existing
tests" step degenerated into writing the first one.

---

## 🔴 D1 — A 403 still renders "No approval chains yet", and it dominates

Real DOM captured from the run with `chains: 403`:

```html
<p class="mb-4 text-[13px] text-danger-600" role="alert">Departments are admin-only.</p>
<section class="rounded-md border border-line-200 bg-surface-0 p-6 shadow-card">
  <div class="py-6 text-center">
    <h3 class="text-base font-semibold">No approval chains yet</h3>
    <p class="mt-1 text-[13px] text-ink-600">Create one to enable requisition submission.</p>
  </div>
</section>
```

Not an aesthetic quibble. The error is **one 13px line of unadorned text**; the empty state is a
**bordered, shadowed card with a 16px semibold centred heading and `py-6` padding** — and its
content is **false**: it asserts the list is empty when the list is *unknown*, and invites an
action ("Create one") that will 403 too. `:173` gates on `chains?.length === 0` with no knowledge
of whether that `[]` came from the server or from the catch.

**Against the dispatch's own stated criterion — "shows both in a way where the empty state
dominates" — the original bug survives.**

## 🔴 D2 — Two clicks erase the error entirely; the page becomes byte-identical to the pre-fix bug

`:163` — `onClick={() => { setShowForm(false); setError(null); }}` clears `error`
**unconditionally**, including a *load* error it did not cause.

Driven flow: load 403s → error visible → click **New chain** → click **Cancel** →
`error === null`, `chains === []`, empty-state card back, **nothing on screen indicates a failure
occurred.** Exactly the state M1 existed to eliminate, reachable in two clicks with no reload.
`submit()` at `:54` does the same `setError(null)`.

---

## 🟡 findings

- **D3 — one `error`, three writers.** With `chains` and `departments` both rejecting, the observed
  on-screen text was **"Departments are admin-only."** On a page titled *Approval chains*, showing
  the empty-chains card, the user is told about departments. Ordering non-deterministic.
- **D4 — create form offered with a one-option dropdown** after `/users/selectable` 403s (only
  `Select approver…`). Good news: constraint validation holds — **no POST is issued**, so no
  confusing server error. The user gets a native tooltip and no reason.
- **D5 — a departed approver renders as a bare GUID** (`3f9a1c22-7b4d-…`). Judged 🟡 not 🔴: it is
  unreadable and unactionable, but *it does not lie about state*. `?? 'Unknown approver (no longer
  active)'` fixes it.
- **D6 — a whitespace-only `displayName` loses the approver row entirely.** `??` is nullish-only, so
  `'   '` wins the lookup and the GUID fallback never fires. Observed cell text after `.trim()`: `''`.
  **Strictly worse than D5** — step number, label, `·`, then nothing.
- **D7 — the real production 403 message is `Request failed (403)`.** Driven through the actual
  `apiFetch` with ASP.NET's default empty-body 403. Visible (the requirement was met) but names
  neither permissions nor which of the three loads failed.
- **D8 — an empty `detail` makes the alert vanish completely.** `{"title":"Forbidden","detail":"","status":403}`
  → `message === ''` → `{error && …}` renders nothing. Proven at the api layer *and* end-to-end.
  A one-line path back to total invisibility.
- **D9 — an empty 2xx body renders a blank page.** `chains === undefined`: not `null`, not
  `length === 0`, so header-only — no list, no empty state, no `Loading…`, no error.

## Attacks that held — no defect

Zero-step chain (`:205` guard correct); 5,000-char chain name; **Zawgyi Burmese**
(`ျမန္မာ အတည္ျပဳခ်က္`) renders intact; non-`Error` rejection falls back correctly; unmount
mid-flight produces no `act()` warnings; POST rejection surfaces the server message; and
**Admin is inside `RecruitmentStaff`** (`Program.cs:61-62`), so the endpoint switch does not lock
the page's own role out of the picker — **that part of the fix is sound.**

## Mutation test — the tests demonstrably fail against broken code

| Mutation | Result |
|---|---|
| **A.** Revert all three catches to the pre-fix silent versions | `8 passed → 5`, `7 failed → 12`. Newly red: *shows the server error text*, *falls back to a canned message*, *surfaces an approver-load failure even when chains load fine* |
| **B.** `chain.steps.length > 0` → `>= 0` at `:205` | zero-step test → RED (`found <ol`) |
| **C.** `setError(err…)` → `setError(null)` in `submit`'s catch **and** drop `required` from the approver `<select>` | both submit tests → RED |

## Caveats declared

No live browser (jsdom + mocked `api` throughout); backend authorization axis not tested (M2).
D1's "dominates" judgement rests on rendered class names and the semantic falsehood of the copy,
**not** on a pixel measurement.

## ⚠️ Conflict for the Orchestrator to resolve

This challenger states all 9 red tests are **intentional defect pins**.
`reviewer_m1_2` independently diagnosed the same file's failures as an **RTL cleanup artifact** —
`frontend/internal/vitest.config.ts:16` sets no `globals`, so auto-`cleanup` is never registered,
and two tests that fail in a whole-file run **pass in isolation** (`-t`).

Both can be partly true. The remediation Worker must separate them: after D1/D2 are fixed, the
tests pinning them must go **green**; any test still red is a cleanup artifact and must be fixed in
the test file. **The tree must not be left red either way.**
