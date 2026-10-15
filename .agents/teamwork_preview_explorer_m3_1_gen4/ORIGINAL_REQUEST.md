## 2026-07-30T02:01:52Z
<USER_REQUEST>
You are Explorer 1 for Milestone 3 (Dynamic Permission Evaluator Engine) of RecruitOps.
Your working directory is: c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_explorer_m3_1_gen4

Task Objective:
Investigate and produce a detailed architectural specification for the Dynamic Permission Evaluation Engine in RecruitOps backend (.NET 10).

Scope & Inputs:
1. Inspect `backend/src/Api/Program.cs`, `backend/src/Infrastructure/DependencyInjection.cs`, `backend/src/Infrastructure/Security/` or `backend/src/Api/Authorization/` (check existing policies in `Policies.cs`).
2. Analyze how custom authorization policies or `[HasPermission("permission:module:feature:action")]` attribute, `PermissionRequirement`, `PermissionAuthorizationHandler`, or custom `IAuthorizationPolicyProvider` should be implemented in ASP.NET Core .NET 10.
3. Determine how user permissions are evaluated from claims, database, or cached user role/permission mapping, taking Super-Admin (`IsSuperAdmin == true`) cross-tenant bypass into account.
4. Ensure Super-Admin bypass works cleanly (e.g. if `User.IsSuperAdmin` is true, permission check passes regardless of specific permissions).
5. Document exact class structures, interfaces, DI registrations, attribute syntax, claims extraction (`User.Claims`, `tenant_id`, `role`, permissions), and caching or db lookups.

Output:
Write a comprehensive report in `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_explorer_m3_1_gen4\handoff.md` and update progress.md in your directory.
Send a message back to parent when complete.
</USER_REQUEST>
