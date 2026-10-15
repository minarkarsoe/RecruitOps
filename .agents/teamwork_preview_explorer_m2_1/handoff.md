# Handoff Report: Requirement R2 (Granular Dynamic RBAC Data Model & Super-Admin)

## 1. Observation

1. **`User.cs` (`backend/src/Domain/Entities/User.cs`, lines 1-19)**:
   ```csharp
   public class User : BaseEntity, ITenantScoped
   {
       public Guid TenantId { get; set; }
       public string Email { get; set; } = string.Empty;
       public string DisplayName { get; set; } = string.Empty;
       public string PasswordHash { get; set; } = string.Empty;
       public UserRole Role { get; set; } = UserRole.Recruiter;
       public bool IsActive { get; set; } = true;
   }
   ```
2. **`UserRole.cs` (`backend/src/Domain/Enums/UserRole.cs`, lines 5-21)**:
   - Defined `enum UserRole` with 5 values: `Admin`, `HrDirector`, `Recruiter`, `HiringManager`, `Approver`.
3. **`AppDbContext.cs` (`backend/src/Infrastructure/Persistence/AppDbContext.cs`, lines 104-115, 382)**:
   - `User.Role` property configured as string: `e.Property(x => x.Role).HasConversion<string>().HasMaxLength(30);`.
   - Global query filter applied: `builder.Entity<User>().HasQueryFilter(e => e.TenantId == _tenant.TenantId);`.
   - `StampTenantAndTimestamps()` auto-populates `TenantId = _tenant.TenantId` for `ITenantScoped` entities when `TenantId == Guid.Empty`.
4. **Baseline Test Status**:
   - `dotnet test backend/RecruitOps.sln` passed all 172 tests (39 in `Domain.Tests`, 133 in `Api.Tests`) with 0 failures.

---

## 2. Logic Chain

1. **Observation 1 & 2**: Currently `User.Role` is locked to an enum (`UserRole`), which prevents runtime addition of custom tenant roles or granular permission assignments.
2. **Requirement R2 Goal**: Support dynamic custom roles per tenant, granular permissions (`permission:<module>:<feature>:<action>`), and Super-Admin cross-tenant capabilities while remaining backwards-compatible with existing enum-based code.
3. **Observation 3 (Multi-Tenancy & Query Filters)**: `AppDbContext` currently filters all `ITenantScoped` entities by `_tenant.TenantId`.
   - Custom roles belong to a tenant (`TenantId = <TenantGuid>`).
   - System-wide roles (including Super-Admin and default template roles) do not belong to a single tenant (`TenantId = null`).
   - Therefore, `Role` cannot use a simple `ITenantScoped` filter. Its query filter must be configured as `e.TenantId == null || e.TenantId == _tenant.TenantId` so system roles are accessible to all tenants while custom roles remain isolated.
4. **Super-Admin Design**:
   - System Super-Admin role is defined with `Code = "super_admin"`, `TenantId = null`, `IsSystemRole = true`, and `IsSuperAdmin = true`.
   - `User.IsSuperAdmin` boolean property added to `User` entity for fast-path cross-tenant authorization checks.
5. **Backwards Compatibility Strategy**:
   - `User.cs` retains `public UserRole Role { get; set; }` and adds `public Guid? RoleId { get; set; }` (FK to `Role`) and `public bool IsSuperAdmin { get; set; }`.
   - System seeds standard roles (`super_admin`, `admin`, `hr_director`, `recruiter`, `hiring_manager`, `approver`).
   - Users associated with standard system roles maintain transparent mapping between `UserRole` enum and dynamic `Role.Code`.

---

## 3. Caveats

1. **DB Migration Data Seeding**: Adding `RoleId` to `User` requires an EF Core migration script or database initializer step to backfill `RoleId` for existing `User` records by mapping their string `Role` column to the corresponding system `Role.Id`.
2. **Application Layer Permission Evaluation**: This report focuses on the Domain Data Model entities (`Role`, `Permission`, `RolePermission`, `User` modifications) and DbContext mappings. The application-layer `IPermissionEvaluator` service and policy handlers will consume these entities in subsequent implementation tasks.

---

## 4. Conclusion

The designed domain model for Requirement R2 comprises:
- `Role` entity (`Id`, `TenantId` [nullable], `Name`, `Code`, `Description`, `IsSystemRole`, `IsSuperAdmin`, `IsActive`, `CreatedAt`)
- `Permission` entity (`Id`, `Module`, `Feature`, `Action`, `Name`, `Description`, `Code`, `CreatedAt`)
- `RolePermission` entity (`RoleId`, `PermissionId`, `AssignedAt`)
- Updated `User` entity (`RoleId` FK, `IsSuperAdmin` flag, retaining `UserRole Role` for backwards compatibility)
- `AppDbContext` configuration including query filters for system & custom roles (`TenantId == null || TenantId == _tenant.TenantId`).

---

## 5. Verification Method

1. **Inspect Artifact Files**:
   - `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_explorer_m2_1\analysis.md`
   - `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_explorer_m2_1\handoff.md`
2. **Execute Baseline Solution Build & Tests**:
   - Command: `dotnet test backend/RecruitOps.sln`
   - Expected Output: All 172 tests pass.
