## 2026-07-29T16:25:02Z
You are Explorer 1 for Milestone 2 of RecruitOps.
Your working directory is: c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_explorer_m2_1
Project root: c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps

Objective:
Investigate and design the domain entities for Requirement R2 (Granular Dynamic RBAC Data Model & Super-Admin):
1. Inspect `backend/src/Domain/Entities/User.cs`, `UserRole.cs` (enum), and EF Core DbContext in `backend/src/Infrastructure/Persistence/`.
2. Design new Domain entities:
   - `Role` (Id, TenantId [nullable for system SuperAdmin roles or scoped for custom tenant roles], Name, Code, Description, IsSystemRole, IsActive, CreatedAt)
   - `Permission` (Id, Module, Feature, Action, Name, Description, Code [e.g. permission:requisitions:approval:approve])
   - `RolePermission` (RoleId, PermissionId)
   - Super-Admin representation: Super-Admin system role that has cross-tenant permissions (`TenantId = null` or `IsSuperAdmin` property on `Role` / `User`).
3. Ensure backwards compatibility so `User.Role` enum maps transparently to standard system roles.

Output:
Write report to `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_explorer_m2_1\analysis.md` and `handoff.md`. Send a message when finished.
