## 2026-07-30T02:20:56Z
You are Worker M4 for Milestone 4 (Frontend User Management, Role Builder & Super-Admin UI) of RecruitOps.
Your working directory is: c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_worker_m4_1_gen4

MANDATORY INTEGRITY WARNING:
DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A Forensic Auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.

Task Objective:
Implement the complete Frontend User Management Screen, Role Builder Permission Matrix UI, and Super-Admin Views in `frontend/internal`.

Architectural Specification to follow:
`c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_explorer_m4_1_gen4\handoff.md`

Implementation Details:
1. **TypeScript Interfaces & API Services**:
   - Define types in `frontend/internal/src/types/` (Permission, Role, User, CreateRoleRequest, UpdateRoleRequest, CreateUserRequest, UpdateUserRequest, PagedResult).
   - Implement API services in `frontend/internal/src/services/` (`roleService.ts`, `userService.ts`, `permissionService.ts`) consuming backend M3 endpoints (`/api/permissions`, `/api/roles`, `/api/users`).

2. **User Management Screen**:
   - Create `UsersPage.tsx` (or `UserManagementPage.tsx`): Table view with pagination, search by email/displayName, role filter, active filter, Create User modal, Edit User modal, and Deactivate/Reactivate toggles with confirmation.

3. **Role Builder Matrix UI**:
   - Create `RolesPage.tsx` (or `RoleManagementPage.tsx`) and `PermissionMatrixGrid.tsx`:
     - Interactive matrix grid grouped by Module (9 modules) and Feature with action operation checkboxes (Read, Create, Update, Delete, Special Actions: Approve, Publish, Cancel, BlindEvaluation).
     - Custom Role Creation & Editing form/modal.
     - System Role read-only view mode (`IsSystemRole == true`).

4. **Super-Admin Views & Navigation**:
   - Add tenant switching / cross-tenant indicators for Super-Admin users.
   - Configure React Router routes in `frontend/internal/src/App.tsx` or router setup for `/users` and `/roles`.

5. **Testing & Build Verification**:
   - Run type check (`npx tsc --noEmit` or `npm run typecheck`) in `frontend/internal`.
   - Run unit/component tests (`npm run test` or `npx vitest run`) in `frontend/internal`.
   - Ensure all type checks and tests pass with 0 errors.

Output:
Write a detailed handoff report in `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_worker_m4_1_gen4\handoff.md` with build and test command outputs. Update progress.md in your directory.
Send a message back to parent when complete.
