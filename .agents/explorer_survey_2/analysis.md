# Requirement R2 Analysis Report: Application Layout & Global Navigation

## Executive Summary
This analysis details the current architecture and proposed redesign for **Requirement R2 (Application Layout & Global Navigation)** in the RecruitOps internal frontend (`frontend/internal/src`).

The current `AppLayout.tsx` provides a functional but basic shell: a fixed 240px sidebar and a top super-admin tenant switcher. It lacks a global header, dynamic breadcrumbs, a global command palette (`Ctrl+K`), high-density Linear/Ashby-style sidebar grouping, and permission-aware quick action toolbars.

This report outlines the codebase findings, structural dependencies, component hierarchy, state & routing context integration, keyboard event handling strategy, and a concrete implementation roadmap.

---

## 1. Codebase Inventory & Current Implementation Analysis

### 1.1 Existing Layout Components
| File Path | Description | Key Responsibilities |
| text | text | text |
| `frontend/internal/src/components/AppLayout.tsx` | Main application shell | Renders `TenantSwitcherBar`, fixed sidebar (`aside.w-60`), navigation links, user profile block, and `<Outlet />`. |
| `frontend/internal/src/components/TenantSwitcherBar.tsx` | Super-Admin Tenant Switcher | Renders banner for `SuperAdmin` users to switch active tenant context. |
| `frontend/internal/src/components/RequireAuth.tsx` | Client-side auth guard | Redirects unauthenticated users to `/login`. |
| `frontend/internal/src/components/RequirePermission.tsx` | RBAC route guard | Checks `hasPermission(session, permission)` and renders 403 fallback if denied. |
| `frontend/internal/src/lib/auth.ts` | Session & RBAC helper | Manages `sessionStorage` session object, permission checks (`hasPermission`), and role predicates (`isSuperAdmin`, `isRecruitmentStaff`, `canApprove`, `isAdmin`). |

### 1.2 Route Configuration (`frontend/internal/src/App.tsx`)
`App.tsx` configures all routes nested under `RequireAuth` > `AppLayout`:
- `/requisitions` — Requisition list (`permission:requisitions:requisitions:read`)
- `/requisitions/new` — Create Requisition (`permission:requisitions:requisitions:create`)
- `/requisitions/:id` — Requisition detail
- `/requisitions/:id/edit` — Edit Requisition
- `/jobpostings` — Job postings & candidate pipeline (`permission:postings:postings:read`)
- `/jobpostings/:id` — Job posting detail
- `/interviews/:id` — Interview round detail & scorecard evaluation
- `/inbox` — Approver inbox (`permission:requisitions:requisitions:approve`)
- `/jdtemplates` — Job description templates (`permission:requisitions:requisitions:read`)
- `/scorecardtemplates` — Scorecard templates (`permission:scorecards:scorecards:manage_templates`)
- `/approvalchains` — Approval chain configuration (`permission:settings:settings:read`)
- `/departments` — Department management (`permission:settings:settings:read`)
- `/users` — User directory (`permission:users:users:read`)
- `/roles` — Role builder & permission matrix (`permission:roles:roles:read`)

---

## 2. Identified Gaps & Deficiencies in Current Shell

1. **No Global Header Bar**:
   - `AppLayout.tsx` does not render a top header. Individual pages render their own page headers (`<header className="mb-6 ...">`), resulting in redundant code and inconsistent layout structures.
2. **Missing Breadcrumb Navigation**:
   - Users cannot see parent-child route relationships or navigate back via breadcrumbs when viewing detail views (e.g., `/requisitions/:id`, `/jobpostings/:id`, `/interviews/:id`).
3. **No Command Palette Integration (`Ctrl+K`)**:
   - There is no global modal search/jump tool. `CommandPalette` component and keyboard event handlers are completely missing.
4. **Basic Un-Grouped Sidebar**:
   - The current sidebar renders links in a single flat list. High-density CRM layouts (Ashby / Linear style) require categorised sections (e.g., *RECRUITING*, *TEMPLATES*, *ADMINISTRATION*) with sleek visual indicators for active states.
5. **No Permission-Aware Global Action Toolbar**:
   - Common actions like "+ New Requisition" or "+ Job Posting" are isolated on individual pages. A global header button/dropdown with permission checking increases usability.

---

## 3. Target Architecture & Design Specification

### 3.1 Component Hierarchy
```
AppLayout (Root Shell)
├── TenantSwitcherBar (SuperAdmin context banner)
├── Sidebar (High-density collateral nav)
│   ├── Brand & Workspace Header
│   ├── Nav Group: RECRUITING (Requisitions, Job Postings, Inbox)
│   ├── Nav Group: TEMPLATES (JD Templates, Scorecard Templates)
│   ├── Nav Group: ADMINISTRATION (Approval Chains, Departments, Users, Roles)
│   └── User & Department Footer Card (Display Name, Role Pill, SuperAdmin Badge, Sign Out)
├── Header (Top Global Navigation Bar)
│   ├── Breadcrumbs (Dynamic route path hierarchy)
│   ├── CommandPaletteTrigger ("Search or jump to... [Ctrl+K]")
│   ├── PermissionAwareActions ("+ New Requisition", etc.)
│   └── User / Tenant Quick Switcher
├── Main Workspace Container (`<main className="...">`)
│   └── Outlet (Page content rendered here)
└── CommandPalette (Modal overlay triggered by Ctrl+K / button click)
```

### 3.2 Dynamic Breadcrumbs Design (`Breadcrumbs.tsx`)
Maps `useLocation().pathname` to human-readable breadcrumb items with link trails:
- `/requisitions` → `Requisitions`
- `/requisitions/new` → `Requisitions` / `New Requisition`
- `/requisitions/req-123` → `Requisitions` / `Requisition Details`
- `/jobpostings` → `Job Postings`
- `/jobpostings/post-123` → `Job Postings` / `Posting Details`
- `/interviews/int-123` → `Interviews` / `Interview Round`
- `/inbox` → `Inbox`
- `/jdtemplates` → `JD Templates`
- `/scorecardtemplates` → `Scorecard Templates`
- `/approvalchains` → `Settings` / `Approval Chains`
- `/departments` → `Settings` / `Departments`
- `/users` → `Admin` / `Users`
- `/roles` → `Admin` / `Role Builder`

### 3.3 Sleek Collateral Sidebar (`Sidebar.tsx`)
- **Theme & Palette**: Utilises design tokens from `tailwind-preset.js`:
  - Active item: `bg-primary-100 text-primary-700 font-semibold border-l-2 border-primary-600`
  - Inactive item: `text-ink-600 hover:bg-surface-50 hover:text-ink-900`
- **Section Headers**: Small uppercase tracking headers (`text-[11px] font-bold text-ink-400 uppercase tracking-wider`).
- **Permission Awareness**: Each link item is wrapped in `hasPermission(session, permissionCode)` check.

### 3.4 Command Palette Integration (`CommandPalette.tsx`)
- **Keyboard Listener**: Mounted in `AppLayout` via `useEffect`:
  ```typescript
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === 'k') {
        e.preventDefault();
        setIsCommandPaletteOpen((prev) => !prev);
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, []);
  ```
- **Modal Content**:
  - Search input with auto-focus.
  - Grouped command list (Navigation, Actions, Quick Filters).
  - Keyboard navigation (Arrow Up/Down, Enter to select, Escape to close).
  - Permission-aware command filtering (hides commands for which user lacks permission).

---

## 4. Implementation Roadmap for Requirement R2

1. **Create Navigation Primitives & Components**:
   - `frontend/internal/src/components/navigation/Breadcrumbs.tsx`
   - `frontend/internal/src/components/navigation/CommandPalette.tsx`
2. **Create Layout Shell Components**:
   - `frontend/internal/src/components/layout/Sidebar.tsx`
   - `frontend/internal/src/components/layout/Header.tsx`
3. **Update `AppLayout.tsx`**:
   - Wire `Sidebar`, `Header`, `Breadcrumbs`, `CommandPalette`, and `TenantSwitcherBar` into `AppLayout`.
   - Implement `isCommandPaletteOpen` state and `Ctrl+K` key listener.
4. **Verification & Testing**:
   - Confirm all Vitest tests in `AppLayout.test.tsx` pass without regressions.
   - Add unit tests for `Breadcrumbs`, `CommandPalette`, and keyboard shortcut handling.
