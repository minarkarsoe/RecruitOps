# Milestone 2 Review Report: Application Layout & Global Navigation

## 1. Observation
- **Reviewed Files**:
  - `frontend/internal/src/components/AppLayout.tsx`: Updated. Shell component managing `TenantSwitcherBar`, `Sidebar`, `Header`, `<Outlet />`, and global `Ctrl+K`/`Cmd+K` keyboard shortcut listener driving `CommandPalette`.
  - `frontend/internal/src/components/Header.tsx`: Created. Sticky top navigation bar with dynamic route `Breadcrumbs`, command palette trigger with `Ctrl+K` badge indicator, permission-guarded `New Requisition` quick action, and user/department profile badge.
  - `frontend/internal/src/components/Sidebar.tsx`: Created. High-density CRM sidebar with grouped links (`Recruitment`, `Team`, `Governance`), permission-based filtering via `hasPermission(session, permissionCode)`, user profile card with SuperAdmin badge, and sign-out handler.
  - `frontend/internal/src/components/Breadcrumbs.tsx`: Created. Exports `getBreadcrumbsForPath(pathname)` helper and `<Breadcrumbs />` component using `useLocation()`, resolving dynamic paths (`/requisitions/:id` -> `Requisition Details`, `/jobpostings/:id` -> `Posting Details`, `/interviews/:id` -> `Interview Round`, `/requisitions/new` -> `New Requisition`).
  - `frontend/internal/src/components/TenantSwitcherBar.tsx`: Preserved SuperAdmin context banner and tenant switcher dropdown.
  - `frontend/internal/src/components/AppLayout.test.tsx`: Updated. Contains 6 unit tests validating menu item rendering for SuperAdmin, granular role-based link filtering, Ctrl+K keyboard shortcut opening Command Palette, dynamic breadcrumb updates, and command search/navigation.
- **Verification Commands & Results**:
  - `npm run test` in `frontend/internal`:
    - Command: `vitest run`
    - Result: 13 passed (13 test files), 114 passed (114 unit tests).
    - `src/components/AppLayout.test.tsx`: 6/6 tests passed.
  - `npm run typecheck`:
    - All Milestone 2 owned files (`AppLayout.tsx`, `Header.tsx`, `Sidebar.tsx`, `Breadcrumbs.tsx`, `TenantSwitcherBar.tsx`, `AppLayout.test.tsx`) have 0 TypeScript errors.
    - Two TS6133 unused variable warnings exist in M1 test files (`src/components/ui/challenger_m1_2.test.tsx:272` and `src/test/milestone1EmpiricalChallenge.test.tsx:31`).

## 2. Logic Chain
- **Requirement Matching**:
  - **R2 Application Layout & Global Navigation**: Redesigned `AppLayout.tsx` with a collateral grouped sidebar (`Sidebar.tsx`), header breadcrumbs (`Breadcrumbs.tsx`), search command palette trigger + `Ctrl+K` shortcut listener, user/department badge, and permission-aware action buttons.
  - **High-Density CRM Layout**: Modern Ashby/Linear-style layout structure with sticky header, grouped navigation (`Recruitment`, `Team`, `Governance`), and active tab indicator styles.
  - **Dynamic Route Breadcrumbs**: `Breadcrumbs.tsx` parses location pathname into human-readable labels and dynamic ID fallbacks with proper accessibility (`aria-label="Breadcrumb"`, `aria-current="page"`).
  - **Permission-Aware Navigation**: Nav links in `Sidebar.tsx`, header action buttons in `Header.tsx`, and palette items in `AppLayout.tsx` check `hasPermission(session, permissionCode)`.
- **Integrity Audit**:
  - No hardcoded test results, facade implementations, or bypasses were found.
  - Test assertions in `AppLayout.test.tsx` genuinely render `AppLayout` inside a `MemoryRouter` and interact with real components and events.

## 3. Caveats
- Two pre-existing TS6133 unused variable errors exist in Milestone 1 test files (`src/components/ui/challenger_m1_2.test.tsx` and `src/test/milestone1EmpiricalChallenge.test.tsx`). These are outside Milestone 2 scope and do not affect Milestone 2 source or test files.

## 4. Conclusion
- **Verdict**: **APPROVE**
- Milestone 2 (App Layout & Global Navigation) implementation is fully complete, well-architected, highly accessible, permission-aware, and verified clean with 100% passing unit tests (114/114).

## 5. Verification Method
- Run unit test suite in `frontend/internal`:
  ```bash
  cd frontend/internal
  npm run test
  ```
- Run specific layout test file:
  ```bash
  cd frontend/internal
  npx vitest run src/components/AppLayout.test.tsx
  ```
