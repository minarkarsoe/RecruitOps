# Handoff Report: Requirement R2 — EF Core Dynamic RBAC Data Model & Seeding Design

**Task:** Explorer 3 for Milestone 2 (RecruitOps)  
**Working Directory:** `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_explorer_m2_3`  
**Date:** 2026-07-29  

---

## 1. Observation

1. **Existing Persistence Model (`backend/src/Infrastructure/Persistence/AppDbContext.cs`):**
   - The current `AppDbContext` handles tenant isolation via `_tenant.TenantId` global query filters (`builder.Entity<T>().HasQueryFilter(e => e.TenantId == _tenant.TenantId)`).
   - Currently, user roles are tracked via a simple enum `UserRole` on `User` entity (`User.cs:16`: `public UserRole Role { get; set; } = UserRole.Recruiter;`).
   - Fluent API entity configurations are declared inside `OnModelCreating` in `AppDbContext.cs`.
2. **Current Initialization (`DbInitializer.cs` & `DatabaseStartup.cs`):**
   - `DbInitializer.cs` seeds an initial Admin user if `Seed:AdminEmail` and `Seed:AdminPassword` configurations are set.
   - `DatabaseStartup.cs` automatically applies pending EF Core migrations on application startup via `db.Database.MigrateAsync()`.
3. **Target Requirement R2 Objective:**
   - Define entities `Role`, `Permission`, `RolePermission`, and update `User`.
   - Implement EF Core Fluent API configurations with unique indexes on `(TenantId, Code)` for `Role`, `Code` for `Permission`, and `(RoleId, PermissionId)` for `RolePermission`.
   - Set cascade deletes for `Role` -> `RolePermission` and `Permission` -> `RolePermission`, while enforcing `DeleteBehavior.Restrict` on `Role` -> `User` to prevent orphaned/broken user roles.
   - Design `RbacSeedData.cs` and `AppDbContextSeed.cs` for automated, idempotent seeding of permissions and default system roles (`SuperAdmin`, `Admin`, `HrDirector`, `Recruiter`, `HiringManager`, `Approver`, `Interviewer`), and backfilling existing users.

---

## 2. Logic Chain

1. **Multi-Tenancy & Role Code Scope:**
   - To support custom roles per company tenant while preserving default system roles, `Role` must implement `ITenantScoped` with `TenantId`.
   - A unique composite index `(TenantId, Code)` on `Roles` guarantees role code uniqueness within each tenant (e.g. `admin`, `recruiter`, `custom_role`).
2. **Global Permission Taxonomy:**
   - `Permission` represents global capabilities (`permission:<module>:<feature>:<action>`). Permissions are system-wide definitions and carry a unique index on `Permission.Code`.
3. **Many-to-Many Role-Permission Mapping:**
   - `RolePermission` acts as the join entity between `Role` and `Permission`.
   - Unique composite index `(RoleId, PermissionId)` prevents duplicate permission assignments.
   - Cascade delete on `RoleId` and `PermissionId` ensures cleaning up role or permission records automatically removes mapping rows without manual orphan cleanup scripts.
4. **User-Role Association & Backward Compatibility:**
   - `User` is updated to include a nullable `Guid? RoleId` FK and `Role? RoleEntity` navigation property.
   - Retaining the existing `UserRole Role` enum property ensures zero breakages during transition/fallback.
   - `DeleteBehavior.Restrict` on `Role` -> `User` prevents deleting a role while users are assigned to it.
5. **Idempotent Startup Seeding & Migration:**
   - `AppDbContextSeed.cs` runs during system initialization, upserting all system permissions and default roles for the tenant, mapping permissions, and populating `User.RoleId` for any legacy user records where `RoleId` is null.

---

## 3. Caveats

- **SuperAdmin Role Query Filter Handling:**
  - `SuperAdmin` role operations or cross-tenant administrative tasks will need `.IgnoreQueryFilters()` when querying roles or permissions across tenants in services.
- **In-Memory Testing:**
  - In-memory database tests (`UseInMemoryDatabase`) used in unit tests do not enforce relational foreign key delete constraints (`DeleteBehavior.Restrict` / `Cascade`). Integration testing against PostgreSQL/SQLite is required to verify foreign key constraint behavior.
- **Backwards Compatibility Window:**
  - The `User.Role` enum field remains present on `User` entity to allow gradual transition without breaking existing code relying on `user.Role`. Application services in Milestone 3 will consume `RoleId` / dynamic permission queries.

---

## 4. Conclusion

The designed dynamic RBAC model (`Role`, `Permission`, `RolePermission`, `User` FK update) and EF Core persistence plan fulfill all requirements for Requirement R2 in Milestone 2. Detailed code snippets, Fluent API index configurations, seeding scripts, and EF Core migration specs have been documented in `analysis.md`.

---

## 5. Verification Method

To independently verify the EF Core configuration and migration when implemented:
1. **Compilation & Assembly Inspection:**
   Run `dotnet build backend/src/Infrastructure` to verify domain entities and EF Core configuration classes compile cleanly.
2. **EF Core Migration Creation:**
   Run `dotnet ef migrations add Module2DynamicRbac --project backend/src/Infrastructure --startup-project backend/src/Api` to verify migration script generation.
3. **Database Migration & Seeding Verification:**
   Execute `dotnet run --project backend/src/Api` (or execute API integration tests) and inspect PostgreSQL database tables (`Permissions`, `Roles`, `RolePermissions`, `Users`) to verify seeded permissions and default roles.
4. **Test Suite Execution:**
   Run `dotnet test backend/tests/RecruitOps.Api.Tests` to ensure existing API tests pass without regression.
