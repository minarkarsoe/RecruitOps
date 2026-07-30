# Changes Summary — Requirement R2 (Granular Dynamic RBAC Data Model)

## 1. Domain Entities & Enums
- **`backend/src/Domain/Entities/Role.cs`**:
  Created `Role` entity inheriting `BaseEntity` with `Guid? TenantId`, `string Name`, `string Code`, `string Description`, `bool IsSystemRole`, `bool IsSuperAdmin`, `bool IsActive`, `ICollection<RolePermission> RolePermissions`, and `ICollection<User> Users`.
- **`backend/src/Domain/Entities/Permission.cs`**:
  Created `Permission` entity inheriting `BaseEntity` with `string Module`, `string Feature`, `string Action`, `string Name`, `string Description`, `string Code` (formatted as `permission:<module>:<feature>:<action>`), and `ICollection<RolePermission> RolePermissions`.
- **`backend/src/Domain/Entities/RolePermission.cs`**:
  Created join entity `RolePermission` with `Guid RoleId`, `Role Role`, `Guid PermissionId`, `Permission Permission`, and `DateTimeOffset AssignedAt`.
- **`backend/src/Domain/Entities/User.cs`**:
  Added `Guid? RoleId`, `Role? CustomRole`, `bool IsSuperAdmin` while preserving `UserRole Role` enum property for backwards compatibility.
- **`backend/src/Domain/Enums/UserRole.cs`**:
  Added `SuperAdmin` and `Interviewer` enum values to support system roles.

## 2. Infrastructure & Persistence Configuration
- **`backend/src/Infrastructure/Persistence/AppDbContext.cs`**:
  - Added `DbSet<Role> Roles`, `DbSet<Permission> Permissions`, `DbSet<RolePermission> RolePermissions`.
  - Configured Fluent API in `OnModelCreating`:
    - Composite primary key `(RoleId, PermissionId)` on `RolePermission`.
    - Unique index on `(TenantId, Code)` for `Role`.
    - Unique index on `Code` for `Permission`.
    - Cascade delete on `RolePermission` (`Role` and `Permission`).
    - Restrict delete on `User.CustomRole` (`RoleId`).
    - Multi-tenant query filter on `Role`: `e.TenantId == null || e.TenantId == _tenant.TenantId`.
- **`backend/src/Infrastructure/Persistence/RbacSeedData.cs`**:
  Defined canonical permissions array across 9 modules (`requisitions`, `postings`, `applications`, `interviews`, `scorecards`, `users`, `roles`, `settings`, `system`) and pre-configured role-permission definitions for all 7 default system roles (`SuperAdmin`, `Admin`, `HrDirector`, `Recruiter`, `HiringManager`, `Approver`, `Interviewer`).
- **`backend/src/Infrastructure/Persistence/DbInitializer.cs`**:
  Implemented `SeedPermissionsAndRolesAsync` to idempotently seed permissions and system roles into database and link existing users without `RoleId` to their corresponding seeded `Role`.
- **`backend/src/Infrastructure/Migrations/20260729162915_AddDynamicRbacDataModel.cs`**:
  Generated EF Core database migration `AddDynamicRbacDataModel` for Postgres database schema setup.

## 3. Unit Tests & Project References
- **`backend/tests/RecruitOps.Domain.Tests/RbacDomainTests.cs`**:
  Added unit tests verifying `Role`, `Permission`, `RolePermission`, `User` backwards compatibility, `UserRole` enum values, `RbacSeedData` canonical permissions and role definitions, and `DbInitializer.SeedPermissionsAndRolesAsync` idempotent database seeding and user linking.
- **`backend/tests/RecruitOps.Domain.Tests/PipelineStatusTests.cs`**:
  Updated `Roles_MatchInHouseModel` assertion to include updated `UserRole` enum values.
- **`backend/tests/RecruitOps.Domain.Tests/RecruitOps.Domain.Tests.csproj`**:
  Added project reference to `RecruitOps.Infrastructure.csproj` and package reference to `Microsoft.EntityFrameworkCore.InMemory` for domain & initializer testing.
