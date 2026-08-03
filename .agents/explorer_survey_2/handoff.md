# Handoff Report: Requirement R2 (Application Layout & Global Navigation)

## 1. Observation
- **`AppLayout.tsx` Location**: `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\frontend\internal\src\components\AppLayout.tsx`
  - Current implementation uses a basic 2-column flex layout (`min-h-screen flex flex-col`): `TenantSwitcherBar` at top, fixed `aside.w-60` sidebar with flat link list, and `<main>` rendering `<Outlet />`.
- **`TenantSwitcherBar.tsx` Location**: `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\frontend\internal\src\components\TenantSwitcherBar.tsx`
  - Displays a SuperAdmin amber context bar at lines 46-107 with hardcoded tenant list (`DEFAULT_TENANTS`) and custom tenant input form.
- **Auth & Permission Utilities**: `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\frontend\internal\src\lib\auth.ts`
  - `auth.get()` manages session from `sessionStorage`.
  - `hasPermission(session, code)` evaluates granular permission codes.
  - Role predicates (`isSuperAdmin`, `isRecruitmentStaff`, `canApprove`, `isAdmin`) mirror backend scopes.
- **Route Definitions**: `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\frontend\internal\src\App.tsx`
  - 14 nested routes wrapped inside `RequireAuth` > `AppLayout`.
- **Existing Unit Tests**: `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\frontend\internal\src\components\AppLayout.test.tsx`
  - Tests menu item visibility for `SuperAdmin`, `Recruiter`, and custom role permissions.
- **Gaps Observed**:
  - No `Header` component or dynamic `Breadcrumbs` component exists in `frontend/internal/src/components/`.
  - No `CommandPalette` component or `Ctrl+K` keyboard event handlers exist anywhere in the codebase.
  - Sidebar links are flat and un-categorised (lacks Ashby/Linear high-density grouping).

---

## 2. Logic Chain
1. *Observation*: `AppLayout.tsx` (lines 28-97) currently only renders a flat list of `NavLink` items and `<Outlet />` inside main.
   *Reasoning*: Pages currently have to implement their own headers independently, causing header inconsistency and duplication.
2. *Observation*: R2 acceptance criteria require a global Ctrl+K search command palette, header breadcrumbs, sleek collateral sidebar, department/user switcher, and permission-aware action buttons.
   *Reasoning*: To fulfill R2, `AppLayout.tsx` must be refactored into a cohesive shell comprising `Header`, `Breadcrumbs`, `Sidebar` (with grouped navigation), `TenantSwitcherBar`, and `CommandPalette`.
3. *Observation*: `AppLayout.test.tsx` verifies that `AppLayout` renders navigation links matching session permissions (e.g. `Requisitions`, `Job postings`, `Inbox`, `Users`, `Role Builder`).
   *Reasoning*: Any redesign of the layout must preserve all existing test query labels so that `AppLayout.test.tsx` continues to pass cleanly with 0 Vitest failures.
4. *Observation*: The `Ctrl+K` event handler requires listening to global `keydown` events on `window` and toggling `isCommandPaletteOpen` modal state in `AppLayout`.
   *Reasoning*: Mounting the event listener at the `AppLayout` level ensures the command palette is globally accessible from any route within the application shell.

---

## 3. Caveats
- **Scope Limit**: As Explorer 2, this investigation is read-only. No code changes have been committed to application source files.
- **CommandPalette UI Placement**: `CommandPalette` can either be placed in `frontend/internal/src/components/navigation/CommandPalette.tsx` or as a reusable primitive in `packages/ui` / `frontend/internal/src/components/ui/CommandPalette.tsx`. Both approaches are compatible.
- **Backend API Integration for Global Search**: The `CommandPalette` initially operates as a route jump and action trigger using client-side route maps, but can be connected to search endpoints (`/api/requisitions`, `/api/jobpostings`) as search services evolve.

---

## 4. Conclusion
Requirement R2 is clearly scoped and ready for implementation. The current `AppLayout.tsx` provides a solid starting baseline, and the permission model (`lib/auth.ts`) and route structure (`App.tsx`) are already well-established. Implementing R2 requires creating `Breadcrumbs`, `CommandPalette`, `Sidebar`, and `Header` components, and assembling them in `AppLayout.tsx` while ensuring existing tests in `AppLayout.test.tsx` pass.

---

## 5. Verification Method
To independently verify this survey and future implementation:
1. **Inspect Report Files**:
   - `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\explorer_survey_2\analysis.md`
   - `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\explorer_survey_2\handoff.md`
2. **Run Test Command**:
   - Run `npm test` inside `frontend/internal` (or `npx vitest run src/components/AppLayout.test.tsx`) to verify layout test suite passes.
3. **Run Typecheck**:
   - Run `npm run typecheck` across workspaces to confirm 0 TypeScript errors.
