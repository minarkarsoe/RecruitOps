## 2026-07-29T23:32:49Z
You are Explorer 1 for Milestone 3 (Dynamic Permission Evaluator Engine & Backend APIs) of RecruitOps.
Your working directory is: c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_explorer_m3_1_gen3

Your task:
Investigate the dynamic permission evaluation requirements for Milestone 3 (Requirement R3) in the RecruitOps backend (.NET 10 Clean Architecture).
Specifically:
1. Inspect existing ASP.NET Core authorization infrastructure (`backend/src/Infrastructure`, `backend/src/Api`, `Program.cs`, `TestAuthHandler.cs`).
2. Examine the dynamic RBAC data model added in Milestone 2 (`Role`, `Permission`, `RolePermission`, `User.RoleId`, `User.IsSuperAdmin`, `RbacSeedData.cs`).
3. Formulate the concrete architecture and implementation plan for the Dynamic Permission Evaluator Engine:
   - Design custom authorization requirement `PermissionRequirement` and handler `PermissionAuthorizationHandler`.
   - Design dynamic policy provider (`IAuthorizationPolicyProvider` or custom attribute `[RequirePermission("permission:code")]`).
   - How permission evaluation handles Super-Admin (`IsSuperAdmin` bypass / cross-tenant scope).
   - How user claims / DB lookup / caching interact (e.g. loading permissions into claims vs evaluating DB per request or caching user permissions).
   - How existing endpoints currently enforcing legacy role checks or missing permission checks will be retrofitted or bridged.
   - Integration with `TestAuthHandler` for API integration tests.
4. Document your investigation, concrete code locations, step-by-step implementation design, and verification plan in `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_explorer_m3_1_gen3\handoff.md`.
5. Send a completion message to the parent orchestrator (conversation ID: 38c03e9d-4038-4d8b-b3c8-4b79a4345671) referencing your handoff report path.
