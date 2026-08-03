# Milestone 2 Reviewer Handoff Report: Application Layout & Global Navigation

## 1. Observation
- **Files Reviewed**:
  - `frontend/internal/src/components/AppLayout.tsx`: Implements top layout shell with `TenantSwitcherBar`, collateral `Sidebar`, sticky `Header`, content `<Outlet />`, and global `Ctrl+K` / `Cmd+K` keyboard event listener controlling `CommandPalette`.
  - `frontend/internal/src/components/Header.tsx`: Implements top header containing `Breadcrumbs`, search trigger button with `Ctrl+K` badge, optional `New Requisition` quick action button (permission-gated), and user profile badge.
  - `frontend/internal/src/components/Sidebar.tsx`: Implements collateral grouped navigation (`Recruitment`, `Team`, `Governance`), active state styling (`bg-primary-100`, `border-primary-600`), permission filtering via `hasPermission`, user card with `Super` admin pill, and sign-out action.
  - `frontend/internal/src/components/Breadcrumbs.tsx`: Implements `getBreadcrumbsForPath(pathname)` parsing URL path segments into human-readable titles (e.g. `/requisitions/req-123/edit` -> `Home > Requisitions > Requisition Details > Edit`) and renders accessible `<nav aria-label="Breadcrumb">`.
  - `frontend/internal/src/components/TenantSwitcherBar.tsx`: Implements top banner for `SuperAdmin` context with default tenant selection menu and custom tenant ID input.
  - `frontend/internal/src/components/AppLayout.test.tsx`: Contains 6 unit tests (3 legacy permission tests + 3 new tests for `Ctrl+K`, dynamic breadcrumbs, command palette search/navigation).
- **Test Execution Output (`npm run test` in `frontend/internal`)**:
  ```
  ✓ src/lib/scorecard.test.ts (14 tests)
  ✓ src/components/RequirePermission.test.tsx (2 tests)
  ✓ src/components/TenantSwitcherBar.test.tsx (3 tests)
  ✓ src/components/PermissionMatrixGrid.test.tsx (4 tests)
  ✓ src/pages/RolesPage.test.tsx (3 tests)
  ✓ src/pages/UsersPage.test.tsx (3 tests)
  ✓ src/components/ApplicationNotes.test.tsx (6 tests)
  ✓ src/components/ui/primitives.test.tsx (18 tests)
  ✓ src/components/ui/challenger_m1_2.test.tsx (10 tests)
  ✓ src/test/milestone4EmpiricalChallenge.test.tsx (15 tests)
  ✓ src/test/milestone1EmpiricalChallenge.test.tsx (23 tests)
  ✓ src/pages/InterviewDetailPage.test.tsx (7 tests)
  ✓ src/components/AppLayout.test.tsx (6 tests)

  Test Files  13 passed (13)
       Tests  114 passed (114)
  ```
- **Typecheck Execution Output (`npm run typecheck`)**:
  - All source files modified for M2 (`AppLayout.tsx`, `Header.tsx`, `Sidebar.tsx`, `Breadcrumbs.tsx`, `TenantSwitcherBar.tsx`, `AppLayout.test.tsx`) are completely type-safe with zero TypeScript errors.
  - Pre-existing TS6133 errors detected in Milestone 1 test files:
    - `src/components/ui/challenger_m1_2.test.tsx(272,35)`: unused parameter `'content'`
    - `src/test/milestone1EmpiricalChallenge.test.tsx(31,3)`: unused import `'Skeleton'`
- **Integrity Check**:
  - No hardcoded test responses, dummy facade implementations, or bypasses detected.

## 2. Logic Chain
1. **Requirement Verification**: Milestone 2 calls for redesigning `AppLayout.tsx` with a collateral sidebar, dynamic route breadcrumbs, global `Ctrl+K` search command palette, user/department profile, tenant switcher, and permission-aware actions.
2. **Code Structure & Quality Analysis**:
   - `AppLayout.tsx` cleanly binds the global `keydown` event listener for `(e.metaKey || e.ctrlKey) && e.key.toLowerCase() === 'k'`, preventing default browser behaviors and toggling `CommandPalette`.
   - `Sidebar.tsx` groups navigation into `Recruitment`, `Team`, and `Governance` sections, filtering items via `hasPermission(session, permission)` and hiding empty groups dynamically.
   - `Breadcrumbs.tsx` extracts path segments and transforms dynamic IDs into readable detail titles while providing valid accessible navigation links.
   - `AppLayout.test.tsx` thoroughly verifies permission filtering, `Ctrl+K` shortcut activation, breadcrumbs rendering for subpaths, and command palette route navigation.
3. **Adversarial & Stress-Testing Findings**:
   - Event listener cleanup in `AppLayout.tsx` prevents memory leaks on unmount.
   - Keyboard shortcut comparison uses `.toLowerCase() === 'k'` to ensure CapsLock or Shift keys do not break activation.
   - Tenant switcher returns `null` for non-SuperAdmin users, avoiding unnecessary layout shifts.

## 3. Caveats
- Pre-existing TS6133 unused variable warnings exist in test files from M1 (`src/components/ui/challenger_m1_2.test.tsx` and `src/test/milestone1EmpiricalChallenge.test.tsx`). These are outside M2 scope and left unchanged per ownership rules.

## 4. Conclusion
- **Verdict**: **APPROVE**
- Milestone 2 implementation for Application Layout & Global Navigation is complete, clean, robust, zero-regression, and fully backward-compatible. All 114 Vitest unit tests pass successfully.

## 5. Verification Method
1. Run the test suite:
   ```bash
   cd frontend/internal
   npm run test
   ```
2. Run specific `AppLayout.test.tsx` unit tests:
   ```bash
   cd frontend/internal
   npx vitest run src/components/AppLayout.test.tsx
   ```
3. Inspect `frontend/internal/src/components/AppLayout.tsx` and child components (`Header.tsx`, `Sidebar.tsx`, `Breadcrumbs.tsx`, `TenantSwitcherBar.tsx`) to verify clean separation of concerns and permission handling.
