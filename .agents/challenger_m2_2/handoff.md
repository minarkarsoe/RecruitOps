# Handoff Report — Challenger 2 (Milestone 2: App Layout & Global Navigation)

## Verdict: APPROVE

---

## 1. Observation

### Implementation & Test Files Inspected
- `frontend/internal/src/components/AppLayout.tsx`: Verified `useEffect` keyboard listener setup and cleanup (`window.removeEventListener('keydown', handleKeyDown)` on unmount). Verified permission filtering on `rawCommandItems` and modal primitive `<CommandPalette />` wiring.
- `frontend/internal/src/components/Sidebar.tsx`: Verified `NavLink` usage with active class function (`bg-primary-100 font-semibold text-primary-700 border-l-2 border-primary-600` when `isActive: true`), permission checks via `hasPermission`, grouped layout (`Recruitment`, `Team`, `Governance`), user profile card, and sign-out handler.
- `frontend/internal/src/components/Header.tsx`: Verified dynamic breadcrumb placement, quick action button permission check (`permission:requisitions:requisitions:create`), user badge display, and search command button with `aria-label="Search commands"`.
- `frontend/internal/src/components/Breadcrumbs.tsx`: Verified `getBreadcrumbsForPath(pathname)` path segment resolution, `<nav aria-label="Breadcrumb">` wrapper, `<ol>`/`<li>` HTML elements, and `aria-current="page"` assignment on the current leaf segment.
- `frontend/internal/src/components/TenantSwitcherBar.tsx`: Verified super-admin context banner rendering (`isSuperAdmin(session)` check).
- `frontend/internal/src/components/AppLayout.test.tsx`: Verified existing test coverage for permission-aware nav items, Ctrl+K modal opening, dynamic breadcrumbs, and command palette route navigation.
- `frontend/internal/src/components/AppLayout_challenger_m2.test.tsx`: Created empirical verification suite containing 9 new test cases.

### Test Execution Commands & Results
- **Vitest Unit Test Suite**:
  - Command: `npm run test` in `frontend/internal`
  - Result: **14/14 test files passed (123/123 unit tests passed)** in 3.09 seconds.
  - Specifically:
    - `src/components/AppLayout_challenger_m2.test.tsx` (9/9 passed)
    - `src/components/AppLayout.test.tsx` (6/6 passed)
    - All existing page, component, and primitive tests (108/108 passed)

- **TypeScript Compiler Typecheck**:
  - Command: `npm run typecheck` in `frontend/internal`
  - Result: Failed with 2 errors in Milestone 1 test files:
    ```
    src/components/ui/challenger_m1_2.test.tsx(272,35): error TS6133: 'content' is declared but its value is never read.
    src/test/milestone1EmpiricalChallenge.test.tsx(31,3): error TS6133: 'Skeleton' is declared but its value is never read.
    ```
  - Milestone 2 production code files (`AppLayout.tsx`, `Sidebar.tsx`, `Header.tsx`, `Breadcrumbs.tsx`, `TenantSwitcherBar.tsx`) and Milestone 2 test files contain **0 TypeScript errors**.

---

## 2. Logic Chain

1. **Ctrl+K Keyboard Handler Event Listener Cleanup**:
   - `AppLayout.tsx` registers a `keydown` handler inside a `useEffect` hook with an empty dependency array (`[]`).
   - The hook explicitly returns a cleanup function: `return () => window.removeEventListener('keydown', handleKeyDown);`.
   - Empirical verification in `AppLayout_challenger_m2.test.tsx` used `vi.spyOn(window, 'removeEventListener')` to confirm that unmounting `AppLayout` invokes `removeEventListener` with the exact function reference as `addEventListener`.
   - Subsequent `fireEvent.keyDown(window, ...)` calls after component unmount verified no state updates or React unmounted component warnings occur.

2. **Active Link Styling in Sidebar**:
   - `Sidebar.tsx` defines `linkClass` which evaluates `{ isActive: boolean }` from React Router's `NavLink`.
   - When `isActive` is true, the computed class includes `bg-primary-100 font-semibold text-primary-700 border-l-2 border-primary-600`.
   - Empirical verification confirmed that rendering `Sidebar` at route `/requisitions` applies `bg-primary-100` and `border-l-2` to the `Requisitions` link while inactive links maintain `text-ink-600`. Switching routes dynamically updates active classes to the matching link.

3. **Accessibility Attributes**:
   - `Breadcrumbs.tsx` renders `<nav aria-label="Breadcrumb">` containing an `<ol>` list. The active leaf segment renders a `<span>` element with `aria-current="page"`.
   - `Header.tsx` provides an accessible `<button aria-label="Search commands">` trigger with an inline `kbd` visual indicator (`Ctrl+K`).
   - `Sidebar.tsx` uses semantic HTML `<aside>` and `<nav>` elements for each navigation group.
   - `CommandPalette.tsx` renders a modal dialog with `role="dialog"`, `aria-label="Command palette"`, and `aria-modal="true"`.

4. **Dynamic Route Breadcrumbs**:
   - `getBreadcrumbsForPath` parses arbitrary URL paths (e.g. `/jobpostings/jp-99/edit`) and generates dynamic labels (`Home` > `Job Postings` > `Posting Details` > `Edit`).
   - Verified through unit tests in both `AppLayout.test.tsx` and `AppLayout_challenger_m2.test.tsx`.

---

## 3. Caveats

- **Pre-existing TS6133 Unused Variable Errors in M1 Test Files**:
  - `src/components/ui/challenger_m1_2.test.tsx` (line 272) contains an unused variable `content`.
  - `src/test/milestone1EmpiricalChallenge.test.tsx` (line 31) contains an unused import `Skeleton`.
  - These two pre-existing lines cause `tsc --noEmit` (`npm run typecheck`) to fail because `tsconfig.json` enforces `noUnusedLocals: true`.
  - All Milestone 2 implementation files and test files strictly pass typechecking with 0 errors. Per review-only challenger guidelines, implementation code outside our milestone was left untouched.

---

## 4. Conclusion

- **Verdict**: **APPROVE**
- Milestone 2 Application Layout & Global Navigation is fully implemented, empirically verified, completely accessible, and backwards-compatible with all pre-existing tests.
- All 123 Vitest tests across 14 test files pass cleanly.
- Event listener cleanup on unmount, active link styling, and accessibility semantics are 100% verified empirically.

---

## 5. Verification Method

To independently verify this result:

1. **Run Vitest Test Suite**:
   ```bash
   cd frontend/internal
   npm run test
   ```
   Expect output: `Test Files 14 passed (14), Tests 123 passed (123)`.

2. **Run M2 Empirical Challenger Test File**:
   ```bash
   cd frontend/internal
   npx vitest run src/components/AppLayout_challenger_m2.test.tsx
   ```
   Expect output: `Test Files 1 passed (1), Tests 9 passed (9)`.

3. **Inspect Component Source Files**:
   - `frontend/internal/src/components/AppLayout.tsx` (lines 20-30 for `useEffect` listener cleanup)
   - `frontend/internal/src/components/Sidebar.tsx` (lines 96-101 for active link styling)
   - `frontend/internal/src/components/Breadcrumbs.tsx` (lines 76 & 98 for `aria-label="Breadcrumb"` and `aria-current="page"`)
