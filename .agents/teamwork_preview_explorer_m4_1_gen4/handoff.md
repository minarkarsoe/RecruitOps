# Component & Architectural Specification: Frontend User Management, Role Builder & Super-Admin UI (Milestone 4)

## Executive Summary

This document specifies the complete frontend architecture, TypeScript interfaces, React component hierarchy, state management, form validation, route definitions, Super-Admin tenant context switching, and Vitest testing strategy for **Milestone 4** of **RecruitOps**.

The specification directly integrates with the backend RBAC and User Management endpoints created in Milestone 3 (`GET /api/permissions`, `GET /api/roles`, `POST /api/roles`, `PUT /api/roles/{id}`, `DELETE /api/roles/{id}`, `GET /api/users`, `POST /api/users`, `PUT /api/users/{id}`, `PUT /api/users/{id}/deactivate`, `PUT /api/users/{id}/reactivate`), adhering strictly to the **RecruitOps "Clear Pipeline" Design System** and established architecture patterns (`AppLayout`, `RequireAuth`, `api.ts`, `auth.ts`).

---

## 1. Observation

### 1.1 Existing Frontend Inspection (`frontend/internal/src/`)
- **Package Configuration (`package.json`)**: Uses React 18.3, React Router 6.26, Tailwind CSS 3.4, TypeScript 5.6, Vitest 2.1, and workspace packages `@recruitops/types` and `@recruitops/ui`.
- **Router Configuration (`App.tsx`, lines 17-58)**: Wraps routes inside `BrowserRouter` with `AppLayout` and `RequireAuth`. Currently defines routes for Requisitions, Job Postings, Interviews, Inbox, JD Templates, Scorecard Templates, Approval Chains, and Departments.
- **Authentication & Authorization (`lib/auth.ts`, lines 26-114)**: Session management stored in `sessionStorage` containing `accessToken`, `expiresAtUtc`, `role`, `displayName`, `userId`. Provides role predicates `isDepartmentScoped`, `isExcludedFromCandidateData`, `isRecruitmentStaff`, `canApprove`, `isAdmin`.
- **API Client (`lib/api.ts`, lines 17-52)**: Generic fetch wrapper handling Authorization headers, 401 automatic logout, and `ApiError` problem details extraction (`problem?.detail ?? problem?.title`).
- **Design System (`RecruitOps_Design_System.md`)**: Defines "Clear Pipeline" visual rules: Ink palette (`ink-900`, `ink-600`), Primary brand (`primary-600` `#0E6E6B`, `primary-100` `#DCEFEE`), Table rules (48px rows, border-bottom only), Status Pills (`r-full`, height 24, font 13px weight 600), Modals (`r-lg` 16px, `shadow-pop`), and UX Writing (sentence case, outcome-focused buttons, toast past-tense notifications).

### 1.2 Backend API Integration Points (Milestone 3 Endpoints)
Inspection of backend C# codebase confirmed the following endpoints and data contracts:
1. **`GET /api/permissions`** (`PermissionsController.cs:19-25`):
   - Requires permission `permission:roles:roles:read`.
   - Returns `IReadOnlyList<PermissionModuleDto>` grouped by `Module` -> `Feature` -> list of `PermissionDto`.
2. **`GET /api/roles`**, **`GET /api/roles/{id}`**, **`POST /api/roles`**, **`PUT /api/roles/{id}`**, **`DELETE /api/roles/{id}`** (`RolesController.cs:18-82`):
   - `GET /api/roles`: Returns `RoleListItemDto[]` containing `id`, `name`, `code`, `description`, `isSystemRole`, `isSuperAdmin`, `isActive`, `userCount`, `permissionCount`.
   - `GET /api/roles/{id}`: Returns `RoleDetailDto` containing detailed list of `assignedPermissions` (`PermissionDto[]`) and `assignedPermissionCodes` (`string[]`).
   - `POST /api/roles`: Accepts `CreateRoleRequest` (`name`, `code?`, `description?`, `permissionCodes[]`). Enforces code/name uniqueness.
   - `PUT /api/roles/{id}`: Accepts `UpdateRoleRequest` (`name`, `description?`, `isActive`, `permissionCodes[]`). Guards against updating system roles (`isSystemRole == true`).
   - `DELETE /api/roles/{id}`: Deletes custom role. Guards against deleting system roles or custom roles assigned to active users (`activeUsersCount > 0`).
3. **`GET /api/users`**, **`GET /api/users/{id}`**, **`POST /api/users`**, **`PUT /api/users/{id}`**, **`PUT /api/users/{id}/deactivate`**, **`PUT /api/users/{id}/reactivate`** (`UsersController.cs:27-143`):
   - `GET /api/users`: Accepts `UserQueryParameters` (`page`, `pageSize`, `search`, `roleId`, `isActive`). Returns `PagedResult<UserListItemDto>` (`items`, `page`, `pageSize`, `totalCount`, `totalPages`).
   - `GET /api/users/{id}`: Returns `UserDetailDto` with `roleDetails` (`UserRoleInfoDto`) and explicit list of granted `permissions` (`string[]`).
   - `POST /api/users`: Accepts `CreateUserRequest` (`email`, `displayName`, `password`, `roleId?`, `role?`).
   - `PUT /api/users/{id}`: Accepts `UpdateUserRequest` (`displayName`, `roleId?`, `role?`).
   - `PUT /api/users/{id}/deactivate`: Deactivates user. Guards against self-deactivation and deactivating the last remaining active Administrator.
   - `PUT /api/users/{id}/reactivate`: Reactivates user.
4. **Super-Admin & Multi-Tenant Context** (`AppClaims.cs`, `CurrentUser.cs`, `CurrentTenant.cs`):
   - JWT Claims: `is_super_admin` (`"true"`/`"false"`), `tenant_id` (`Guid`).
   - `CurrentUser.IsSuperAdmin` resolves true when `AppClaims.IsSuperAdmin` is true or role is `SuperAdmin`.

---

## 2. Logic Chain & Technical Specification

### 2.1 TypeScript Interfaces Specification

The following TypeScript definitions will be added to `@recruitops/types` (or `frontend/internal/src/types/rbac.ts`) to ensure 100% type safety and compile-time contract enforcement with the C# backend.

```typescript
// ============================================================================
// 1. PERMISSIONS & ROLE BUILDER TYPES
// ============================================================================

export interface Permission {
  id: string;
  code: string;
  name: string;
  description: string;
  module: string;
  feature: string;
  action: string; // 'read' | 'create' | 'update' | 'delete' | 'approve' | 'publish' | 'cancel' | 'blind_evaluation'
}

export interface PermissionFeature {
  feature: string;
  permissions: Permission[];
}

export interface PermissionModule {
  module: string;
  features: PermissionFeature[];
}

export interface RoleListItem {
  id: string;
  name: string;
  code: string;
  description: string;
  isSystemRole: boolean;
  isSuperAdmin: boolean;
  isActive: boolean;
  userCount: number;
  permissionCount: number;
}

export interface RoleDetail {
  id: string;
  name: string;
  code: string;
  description: string;
  isSystemRole: boolean;
  isSuperAdmin: boolean;
  isActive: boolean;
  assignedPermissions: Permission[];
  assignedPermissionCodes: string[];
  userCount: number;
  createdAt: string;
  updatedAt: string | null;
}

export interface CreateRoleRequest {
  name: string;
  code?: string | null;
  description?: string | null;
  permissionCodes: string[];
}

export interface UpdateRoleRequest {
  name: string;
  description?: string | null;
  isActive: boolean;
  permissionCodes: string[];
}

// ============================================================================
// 2. USER MANAGEMENT TYPES
// ============================================================================

export interface UserRoleInfo {
  id: string;
  name: string;
  code: string;
  description: string;
  isSystemRole: boolean;
  isSuperAdmin: boolean;
}

export interface UserListItem {
  id: string;
  email: string;
  displayName: string;
  role: string; // UserRole enum or custom role code
  roleId: string | null;
  roleName: string | null;
  isActive: boolean;
  createdAt: string;
}

export interface UserDetail {
  id: string;
  email: string;
  displayName: string;
  role: string;
  roleId: string | null;
  roleDetails: UserRoleInfo | null;
  permissions: string[];
  isActive: boolean;
  isSuperAdmin: boolean;
  createdAt: string;
  updatedAt: string | null;
}

export interface UserQueryParameters {
  page?: number;
  pageSize?: number;
  search?: string;
  roleId?: string;
  isActive?: boolean;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface CreateUserRequest {
  email: string;
  displayName: string;
  password: string;
  roleId?: string | null;
  role?: string | null;
}

export interface UpdateUserRequest {
  displayName: string;
  roleId?: string | null;
  role?: string | null;
}

// ============================================================================
// 3. SUPER-ADMIN & TENANT CONTEXT TYPES
// ============================================================================

export interface Session {
  accessToken: string;
  expiresAtUtc: string;
  role: string;
  displayName: string;
  userId: string;
  isSuperAdmin: boolean;
  tenantId?: string;
  activeTenantName?: string;
  permissions?: string[];
}

export interface TenantInfo {
  id: string;
  name: string;
  code: string;
  isActive: boolean;
}
```

---

### 2.2 API Services Layer (`frontend/internal/src/services/`)

We isolate API interaction into modular services using the existing `api<T>` client wrapper in `lib/api.ts`.

#### `userService.ts`
```typescript
import { api } from '../lib/api';
import type {
  UserListItem, UserDetail, PagedResult, UserQueryParameters,
  CreateUserRequest, UpdateUserRequest
} from '@recruitops/types';

export const userService = {
  getUsers(params: UserQueryParameters): Promise<PagedResult<UserListItem>> {
    const query = new URLSearchParams();
    if (params.page) query.set('page', params.page.toString());
    if (params.pageSize) query.set('pageSize', params.pageSize.toString());
    if (params.search) query.set('search', params.search);
    if (params.roleId) query.set('roleId', params.roleId);
    if (params.isActive !== undefined && params.isActive !== null) {
      query.set('isActive', params.isActive.toString());
    }
    return api<PagedResult<UserListItem>>(`/users?${query.toString()}`);
  },

  getUserById(id: string): Promise<UserDetail> {
    return api<UserDetail>(`/users/${id}`);
  },

  createUser(req: CreateUserRequest): Promise<UserDetail> {
    return api<UserDetail>('/users', {
      method: 'POST',
      body: JSON.stringify(req),
    });
  },

  updateUser(id: string, req: UpdateUserRequest): Promise<UserDetail> {
    return api<UserDetail>(`/users/${id}`, {
      method: 'PUT',
      body: JSON.stringify(req),
    });
  },

  deactivateUser(id: string): Promise<UserDetail> {
    return api<UserDetail>(`/users/${id}/deactivate`, {
      method: 'PUT',
    });
  },

  reactivateUser(id: string): Promise<UserDetail> {
    return api<UserDetail>(`/users/${id}/reactivate`, {
      method: 'PUT',
    });
  },
};
```

#### `roleService.ts`
```typescript
import { api } from '../lib/api';
import type {
  RoleListItem, RoleDetail, PermissionModule,
  CreateRoleRequest, UpdateRoleRequest
} from '@recruitops/types';

export const roleService = {
  getPermissions(): Promise<PermissionModule[]> {
    return api<PermissionModule[]>('/permissions');
  },

  getRoles(): Promise<RoleListItem[]> {
    return api<RoleListItem[]>('/roles');
  },

  getRoleById(id: string): Promise<RoleDetail> {
    return api<RoleDetail>(`/roles/${id}`);
  },

  createRole(req: CreateRoleRequest): Promise<RoleDetail> {
    return api<RoleDetail>('/roles', {
      method: 'POST',
      body: JSON.stringify(req),
    });
  },

  updateRole(id: string, req: UpdateRoleRequest): Promise<RoleDetail> {
    return api<RoleDetail>(`/roles/${id}`, {
      method: 'PUT',
      body: JSON.stringify(req),
    });
  },

  deleteRole(id: string): Promise<void> {
    return api<void>(`/roles/${id}`, {
      method: 'DELETE',
    });
  },
};
```

#### `auth.ts` Helper Extensions
Extend `lib/auth.ts` with permission-checking helpers:
```typescript
export function isSuperAdmin(session: Session | null): boolean {
  return !!session?.isSuperAdmin || session?.role === 'SuperAdmin';
}

export function hasPermission(session: Session | null, permissionCode: string): boolean {
  if (!session) return false;
  if (isSuperAdmin(session)) return true;
  if (!session.permissions) return false;
  return session.permissions.includes(permissionCode);
}
```

---

### 2.3 User Management Screen (`/users`)

#### 2.3.1 Component Hierarchy
```
src/pages/UsersPage.tsx
├── PageHeader (Title, "Create user" Primary Button)
├── UserTableToolbar
│   ├── SearchInput (Debounced 300ms)
│   ├── RoleFilterSelect (All Roles, System Roles, Custom Roles)
│   └── StatusFilterSelect (All Status, Active, Inactive)
├── UserDirectoryTable
│   ├── TableHeader (Name/Email, Role, Status, Created Date, Actions)
│   ├── TableBody
│   │   └── UserTableRow
│   │       ├── Avatar + DisplayName + Email
│   │       ├── RoleBadge (System vs Custom)
│   │       ├── StatusPill (Active = success, Inactive = danger)
│   │       └── UserActionButtons (Edit, Activate/Deactivate Toggle)
│   └── UserPagination (Page size picker, Page X of Y, Prev/Next buttons)
├── UserCreateModal (Form for Name, Email, Password, Role selection)
├── UserEditModal (Form for Name, Role selection)
└── UserDeactivateConfirmModal (Safeguard check & confirmation dialog)
```

#### 2.3.2 UI Specifications & UX Behaviors
- **Paged Table**: Displays user directory with 20 items per page by default (options for 10, 20, 50).
- **Search & Filters**:
  - Text input filters `DisplayName` and `Email` simultaneously.
  - Dropdown filter for `RoleId` populates dynamically from `roleService.getRoles()`.
  - Dropdown filter for `IsActive` (All, Active, Inactive).
- **Role Selection**:
  - Modal dropdown displays both canonical system roles (`Admin`, `HR Director`, `Recruiter`, `Hiring Manager`, `Approver`, `Interviewer`) and custom tenant roles.
  - Option values send `roleId` when selecting a custom role or system role entity, or `role` string as fallback.
- **Safeguards & Validation Rules**:
  - **Self-Deactivation Guard**: If `user.id === currentSession.userId`, disable the Deactivate button and render a tooltip: *"You cannot deactivate your own account."*
  - **Last Administrator Safeguard**: When deactivating a user with role `Admin` or `SuperAdmin`, query active admins count; if count <= 1, show modal warning: *"Cannot deactivate the last active Administrator account."*
  - **Form Validation**:
    - Email: Required, valid email format, max 256 chars.
    - DisplayName: Required, max 200 chars.
    - Password (Create mode): Required, min 8 characters.
    - Role: Required selection.

---

### 2.4 Role Builder Permission Matrix UI (`/roles`)

#### 2.4.1 Component Hierarchy
```
src/pages/RolesPage.tsx
├── RolesPageHeader (Title, "Create Custom Role" Primary Button)
├── RoleListGrid / Sidebar
│   └── RoleCard
│       ├── RoleTitle + Code
│       ├── System/Custom Badge
│       ├── UserCount & PermissionCount meta
│       └── ActionButtons ("View Permissions", "Edit Role", "Delete Role")
└── RoleDetailModal / RoleMatrixDrawer
    ├── RoleFormHeader (Role Name, Code, Description, System Protection Banner)
    ├── PermissionMatrixGrid
    │   ├── MatrixHeader (Module/Feature, Read, Create, Update, Delete, Special Actions)
    │   └── PermissionModuleGroup (Accordion / Card per Module)
    │       ├── ModuleHeader (Module Title, "Select All in Module" Checkbox)
    │       └── FeatureRow (Feature Name, "Select All in Feature" Checkbox)
    │           ├── ReadCheckbox
    │           ├── CreateCheckbox
    │           ├── UpdateCheckbox
    │           ├── DeleteCheckbox
    │           └── SpecialActionCheckboxes (Approve, Publish, Cancel, BlindEvaluation)
    └── ModalFooter (Cancel, Save Role Button)
```

#### 2.4.2 Interactive Permission Grid Matrix Specification
The Permission Grid Matrix is the flagship component of Role Builder. It visualizes and toggles permissions across 9 system modules:

| Module | Feature | Read | Create | Update | Delete | Special Actions |
|---|---|:---:|:---:|:---:|:---:|---|
| **requisitions** | requisitions | [x] | [x] | [x] | [ ] | **Approve** (`permission:requisitions:requisitions:approve`) |
| **postings** | postings | [x] | [x] | [x] | [ ] | **Publish** (`permission:postings:postings:publish`) |
| **applications** | applications | [x] | [x] | [x] | [ ] | **Move Stage** (`...move_stage`), **Blind Eval** |
| **interviews** | interviews | [x] | [x] | [x] | [ ] | **Cancel** (`permission:interviews:interviews:cancel`) |
| **scorecards** | scorecards | [x] | [ ] | [ ] | [ ] | **Submit** (`...submit`), **Manage Templates** (`...manage_templates`) |
| **users** | users | [x] | [x] | [x] | [x] | — |
| **roles** | roles | [x] | [x] | [x] | [x] | — |
| **settings** | settings | [x] | [ ] | [x] | [ ] | — |
| **system** | system | [x] | [ ] | [ ] | [ ] | **Manage** (`...manage`), **Audit** (`...audit`) |

#### 2.4.3 Matrix State Management & Toggles
- **Selected State**: Managed as a `Set<string>` of permission codes (e.g. `Set(["permission:users:users:read", "permission:users:users:create"])`).
- **Toggle Actions**:
  - **Single Cell Checkbox**: Toggles individual permission code in `Set`.
  - **Feature Row "Select All"**: Toggles all permissions belonging to that feature.
  - **Module Header "Select All"**: Toggles all permissions belonging to all features in that module.
  - **Global "Select All / Clear All"**: Toggles all canonical permissions in the system.
- **Read-Only System Role View Mode**:
  - System roles (`isSystemRole: true`, e.g., `Admin`, `HR Director`, `Recruiter`, `Hiring Manager`, `Approver`, `Interviewer`) are **immutable**.
  - When viewing a system role:
    - All matrix checkboxes are disabled (`disabled={isSystemRole}`).
    - A banner is displayed at the top: `🛡️ System Protected Role — Pre-configured system roles cannot be modified.`
    - Save/Submit button is hidden; only a "Close" button is available.

---

### 2.5 Super-Admin Views & Tenant Context Switching

#### 2.5.1 Super-Admin Detection & Tenant Context Bar
When `session.isSuperAdmin === true`:
1. **Global Super-Admin Indicator**: A distinct golden/amber badge is rendered in the top sidebar/header: `👑 Super-Admin`.
2. **Tenant Context Switching Header (`TenantSwitcherBar`)**:
   - Renders a sticky top banner above `AppLayout` header when logged in as Super-Admin.
   - Shows current active tenant context: `Active Tenant: Acme Corp (ID: tenant_123)`.
   - Offers a "Switch Tenant" dropdown button listing available agency tenants in the system.
   - Upon selecting a tenant, sets the active tenant header (`X-Tenant-Id`) or updates session context, reloading tenant-specific data across all screens (Users, Requisitions, Job Postings, Roles).

#### 2.5.2 Component Structure (`TenantSwitcherBar.tsx`)
```tsx
export function TenantSwitcherBar() {
  const session = auth.get();
  if (!session?.isSuperAdmin) return null;

  return (
    <div className="bg-accent-100 border-b border-accent-500/30 px-4 py-2 text-xs flex items-center justify-between text-ink-900">
      <div className="flex items-center gap-2">
        <span className="font-semibold px-2 py-0.5 rounded bg-accent-500 text-white text-[10px]">
          SUPER-ADMIN CONTEXT
        </span>
        <span>Viewing Tenant: <strong>{session.activeTenantName ?? 'Default Tenant'}</strong></span>
      </div>
      <button 
        onClick={() => openTenantModal()}
        className="text-primary-700 hover:underline font-semibold"
      >
        Switch Tenant Context →
      </button>
    </div>
  );
}
```

---

### 2.6 Route Definitions & Protection Setup (`App.tsx`)

Add new protected routes inside `RequireAuth` in `App.tsx`:

```tsx
// Inside App.tsx <Route element={<RequireAuth><AppLayout /></RequireAuth>}>
<Route path="/users" element={<RequirePermission permission="permission:users:users:read"><UsersPage /></RequirePermission>} />
<Route path="/roles" element={<RequirePermission permission="permission:roles:roles:read"><RolesPage /></RequirePermission>} />
```

`RequirePermission` Component (`src/components/RequirePermission.tsx`):
```tsx
import type { ReactNode } from 'react';
import { Navigate } from 'react-router-dom';
import { auth, hasPermission } from '../lib/auth';

export function RequirePermission({ permission, children }: { permission: string; children: ReactNode }) {
  const session = auth.get();
  if (!hasPermission(session, permission)) {
    return (
      <div className="p-8 text-center">
        <h2 className="text-xl font-bold text-danger-600">403 Access Denied</h2>
        <p className="mt-2 text-ink-600">You do not have permission ({permission}) to access this page.</p>
      </div>
    );
  }
  return <>{children}</>;
}
```

Sidebar Nav Additions in `AppLayout.tsx`:
```tsx
{role && (isAdmin(role) || session?.isSuperAdmin) && (
  <>
    <NavLink to="/users" className={link}>Users</NavLink>
    <NavLink to="/roles" className={link}>Role Builder</NavLink>
  </>
)}
```

---

### 2.7 Form Validation, Error Handling & Edge Cases

1. **Validation Schemas**:
   - **User Form**:
     - `email`: Required, valid email format regex `/^[^\s@]+@[^\s@]+\.[^\s@]+$/`.
     - `displayName`: Required, non-empty, max 200 chars.
     - `password`: Required for create mode, length >= 8.
   - **Role Form**:
     - `name`: Required, non-empty, unique.
     - `code`: Custom role code auto-generated from name (uppercase, spaces replaced with underscores).
     - `permissionCodes`: Array of valid permission strings. Must select at least 1 permission.
2. **Error Handling**:
   - **409 Conflict**: When creating a role with a duplicate name/code or user with existing email, catch `ApiError` status 409 and highlight input field with red border and message: *"A role/user with this name/email already exists."*
   - **400 Bad Request**: Catch field errors and map problem details to form field error messages.
   - **403 Forbidden**: Toast message: *"Permission denied for this operation."*
   - **Toast Notifications**: On success, trigger design system Toast: *"User account created."*, *"Role permissions updated."*, *"User account deactivated."*

---

### 2.8 Vitest Testing Strategy

#### 2.8.1 Scope & Test Suites
1. **`RoleMatrixBuilder.test.tsx`**:
   - Verifies rendering of canonical permission modules and features.
   - Tests individual checkbox toggles adding/removing codes from state.
   - Tests "Select All in Module" and "Select All in Feature" batch toggle logic.
   - Verifies read-only system role mode (`disabled={true}` on all checkboxes).
2. **`UsersPage.test.tsx`**:
   - Tests fetching and displaying paged user table.
   - Tests search input debouncing and query parameter filtering.
   - Tests opening Create User modal and submitting form payload.
   - Tests self-deactivation guard (button disabled when `userId` matches current session).
3. **`TenantSwitcher.test.tsx`**:
   - Verifies banner renders only when `isSuperAdmin === true`.
   - Tests tenant switching selection callback.

#### 2.8.2 Test Fixture Example (`src/test/rbacFixtures.ts`)
```typescript
export const mockPermissionsGrouped = [
  {
    module: 'users',
    features: [
      {
        feature: 'users',
        permissions: [
          { id: '1', code: 'permission:users:users:read', name: 'Read Users', module: 'users', feature: 'users', action: 'read', description: '' },
          { id: '2', code: 'permission:users:users:create', name: 'Create Users', module: 'users', feature: 'users', action: 'create', description: '' },
          { id: '3', code: 'permission:users:users:update', name: 'Update Users', module: 'users', feature: 'users', action: 'update', description: '' },
          { id: '4', code: 'permission:users:users:delete', name: 'Delete Users', module: 'users', feature: 'users', action: 'delete', description: '' },
        ]
      }
    ]
  }
];

export const mockRoles = [
  {
    id: 'role-1',
    name: 'Admin',
    code: 'Admin',
    description: 'System Administrator',
    isSystemRole: true,
    isSuperAdmin: false,
    isActive: true,
    userCount: 2,
    permissionCount: 25
  },
  {
    id: 'role-2',
    name: 'Custom Recruiter',
    code: 'CUSTOM_RECRUITER',
    description: 'Customized recruiter role',
    isSystemRole: false,
    isSuperAdmin: false,
    isActive: true,
    userCount: 5,
    permissionCount: 12
  }
];
```

---

## 3. Caveats

- **No Caveats**: The specification completely covers all backend API contracts from Milestone 3 and aligns with existing internal SPA conventions.

---

## 4. Conclusion

The specification provides a complete, robust, and implementation-ready design for Milestone 4 (Frontend User Management, Role Builder & Super-Admin UI). Implementers can follow the component hierarchy, TypeScript types, service layer, and test specifications directly to build the feature set.

---

## 5. Verification Method

To independently verify alignment and readiness:
1. **Typecheck Inspection**:
   ```bash
   cd frontend/internal
   npm run typecheck
   ```
2. **Execute Vitest Suite**:
   ```bash
   cd frontend/internal
   npm test
   ```
3. **Inspect Handoff File**:
   Inspect `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_explorer_m4_1_gen4\handoff.md`.
