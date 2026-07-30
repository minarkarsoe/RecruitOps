## 2026-07-29T16:26:10Z
You are Worker 1 for Milestone 2 of RecruitOps.
Your working directory is: c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_worker_m2_1
Project root: c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps

DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A Forensic Auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.

Objective:
Implement Requirement R2 (Granular Dynamic RBAC Data Model, Domain Entities, EF Core Configuration, Seed Data & Migration):

1. **Domain Entities**:
   - `backend/src/Domain/Entities/Role.cs`: Create entity with `Guid? TenantId`, `string Name`, `string Code`, `string Description`, `bool IsSystemRole`, `bool IsSuperAdmin`, `bool IsActive`, navigation `ICollection<RolePermission> RolePermissions`, `ICollection<User> Users`.
   - `backend/src/Domain/Entities/Permission.cs`: Create entity with `string Module`, `string Feature`, `string Action`, `string Name`, `string Description`, `string Code` (`permission:<module>:<feature>:<action>`), navigation `ICollection<RolePermission> RolePermissions`.
   - `backend/src/Domain/Entities/RolePermission.cs`: Join entity with `Guid RoleId`, `Role Role`, `Guid PermissionId`, `Permission Permission`, `DateTimeOffset AssignedAt`.
   - `backend/src/Domain/Entities/User.cs`: Add `Guid? RoleId`, `Role? CustomRole`, `bool IsSuperAdmin`. Keep `UserRole Role` enum property intact for backwards compatibility.
   - `backend/src/Domain/Enums/UserRole.cs`: Add `SuperAdmin` to enum values.

2. **EF Core Configurations & DbContext**:
   - `backend/src/Infrastructure/Persistence/AppDbContext.cs`:
     - Add `DbSet<Role> Roles { get; set; }`
     - Add `DbSet<Permission> Permissions { get; set; }`
     - Add `DbSet<RolePermission> RolePermissions { get; set; }`
     - Fluent API in `OnModelCreating`: Configure primary keys, indexes (`Role.TenantId` + `Role.Code` unique, `Permission.Code` unique), relationships, cascade delete on `RolePermission`, restrict on `User.CustomRole`.
     - Query filter for `Role`: `e.TenantId == null || e.TenantId == _tenant.TenantId`.

3. **Seeding Framework & Initializer**:
   - Create `backend/src/Infrastructure/Persistence/RbacSeedData.cs`: Define canonical permissions array across 9 modules (`requisitions`, `postings`, `applications`, `interviews`, `scorecards`, `users`, `roles`, `settings`, `system`) and standard pre-configured role-permission definitions for all 7 default system roles (`SuperAdmin`, `Admin`, `HrDirector`, `Recruiter`, `HiringManager`, `Approver`, `Interviewer`).
   - Update `backend/src/Infrastructure/Persistence/DbInitializer.cs`: Implement `SeedPermissionsAndRolesAsync` to idempotently seed permissions and system roles into database on startup/seed, and link existing users without a `RoleId` to their corresponding seeded `Role`.

4. **Unit Tests**:
   - Add unit/domain tests in `backend/tests/RecruitOps.Domain.Tests` verifying `Role`, `Permission`, `RolePermission`, and `UserRole` mapping.
   - Verify `dotnet build backend/RecruitOps.sln` and `dotnet test backend/RecruitOps.sln` pass 100%.

Output:
Write report to `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_worker_m2_1\handoff.md` and `changes.md`. Update progress.md and send a message to orchestrator when finished.
