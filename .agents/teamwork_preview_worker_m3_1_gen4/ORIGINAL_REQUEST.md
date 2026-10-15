## 2026-07-30T02:05:19Z
You are Worker 1 for Milestone 3 (Backend Authorization Engine & APIs) of RecruitOps.
Your working directory is: c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_worker_m3_1_gen4

MANDATORY INTEGRITY WARNING:
DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A Forensic Auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.

Task Objective:
Implement the complete backend Dynamic Permission Evaluator Engine, Roles & Permissions Management APIs, and User Account Management APIs in RecruitOps (.NET 10).

Architectural Specifications to follow:
1. Explorer 1 Report: `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_explorer_m3_1_gen4\handoff.md`
2. Explorer 2 Report: `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_explorer_m3_2_gen3\handoff.md`
3. Explorer 3 Report: `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_explorer_m3_3_gen4\handoff.md`

Implementation Tasks:
1. **Dynamic Authorization Engine**:
   - Implement `HasPermissionAttribute`, `PermissionRequirement`, `PermissionAuthorizationHandler`, `PermissionPolicyProvider`, `IPermissionEvaluator` and `PermissionEvaluator` with `IMemoryCache` 2-tier caching.
   - Implement instant Super-Admin (`IsSuperAdmin == true`) cross-tenant bypass in the authorization handler.
   - Register services and authorization handlers in `Infrastructure/DependencyInjection.cs` and `Api/Program.cs`.

2. **Roles & Permissions Management APIs**:
   - Implement `IRoleService` and `RoleService` (`GetPermissionsAsync`, `GetRolesAsync`, `GetRoleByIdAsync`, `CreateRoleAsync`, `UpdateRoleAsync`, `DeleteRoleAsync`).
   - Implement `RolesController` with endpoints:
     - `GET /api/permissions` (`[HasPermission("permission:roles:roles:read")]`)
     - `GET /api/roles` (`[HasPermission("permission:roles:roles:read")]`)
     - `GET /api/roles/{id}` (`[HasPermission("permission:roles:roles:read")]`)
     - `POST /api/roles` (`[HasPermission("permission:roles:roles:create")]`)
     - `PUT /api/roles/{id}` (`[HasPermission("permission:roles:roles:update")]`)
     - `DELETE /api/roles/{id}` (`[HasPermission("permission:roles:roles:delete")]`)
   - Enforce system role immutability (`IsSystemRole == false` check on PUT/DELETE) and active user protection on DELETE (409 Conflict).

3. **User Account Management APIs**:
   - Implement / update `IUserService`, `UserService`, `UsersController` with:
     - `GET /api/users`: Paged query, search by email/displayName, filter by RoleId and IsActive. Use EF Core 10 two-step projection (SQL projection first, in-memory DTO transformation) to avoid LINQ `Enum.ToString()` translation issues.
     - `GET /api/users/{id}`: Detailed user info with custom role details and permission codes.
     - `POST /api/users`: Create user with password hashing and email uniqueness validation.
     - `PUT /api/users/{id}`: Update user metadata and role assignment.
     - `PUT /api/users/{id}/deactivate`: Soft-deactivate user account (with self-deactivation & last active Admin safety checks).
     - `PUT /api/users/{id}/reactivate`: Reactivate user account.
     - Retain `GET /api/users/selectable` for panel picker compatibility.
   - Protect all User Management endpoints with dynamic `[HasPermission]` attributes.

4. **Testing & Verification**:
   - Add/update unit and integration tests in `backend/tests/RecruitOps.Api.Tests` and `backend/tests/RecruitOps.Domain.Tests` covering Roles, Users, and Authorization policies.
   - Execute `dotnet test` on the solution and ensure 100% of tests pass.

Output:
Write a detailed implementation handoff report in `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_worker_m3_1_gen4\handoff.md` containing build/test commands executed and results. Update `progress.md` in your directory.
Send a message back to parent when complete.
