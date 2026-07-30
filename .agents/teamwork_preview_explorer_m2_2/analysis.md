# Milestone 2 (R2): Granular Permission Taxonomy & Seed Matrix Design Report

**Author:** Explorer 2 (Milestone 2)  
**Date:** 2026-07-29  
**Target Path:** `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_explorer_m2_2\analysis.md`  

---

## 1. Executive Summary

Requirement R2 transitions RecruitOps from a static enum-based RBAC model (`UserRole` enum in `RecruitOps.Domain.Enums`) to a fine-grained, dynamic Role-Based Access Control (RBAC) architecture with database-backed roles, granular permission strings, and role-permission assignment matrices.

This report establishes:
1. A standard, hierarchical string permission taxonomy using the format `permission:<module>:<feature>:<action>`.
2. An exhaustive seed matrix for all 7 predefined system roles (`SuperAdmin`, `Admin`, `HrDirector`, `Recruiter`, `HiringManager`, `Approver`, `Interviewer`).
3. An EF Core database schema and idempotent seeding strategy that initializes standard permissions and default role mappings cleanly without breaking existing single-tenant / department-scoped user workflows.

---

## 2. Granular Permission Taxonomy Design

### 2.1 Format Specification
All permission codes in RecruitOps strictly comply with the canonical three-part colon-delimited string format:

$$\text{Code} = \text{\texttt{permission:<module>:<feature>:<action>}}$$

* **`permission`**: Standard root namespace prefix for all authorization claims.
* **`<module>`**: Lowercase name of the functional area (`requisitions`, `postings`, `applications`, `interviews`, `scorecards`, `users`, `roles`, `settings`, `system`).
* **`<feature>`**: Lowercase noun identifying the specific sub-resource or domain aggregate (`requisition`, `posting`, `application`, `interview`, `scorecard`, `user`, `role`, `setting`, `tenant`).
* **`<action>`**: Lowercase verb specifying the authorized operation.
  * *Standard CRUD Actions*: `create`, `read`, `update`, `delete`.
  * *Special Domain Actions*: `approve`, `publish`, `cancel`, `schedule`, `submit`, `blindevaluation`, `assign`, `manage`.

### 2.2 Complete Permission Taxonomy Reference

The table below details all permission codes across the 8 standard functional modules plus the system administration module:

| Module (`<module>`) | Feature (`<feature>`) | Action (`<action>`) | Canonical Permission String (`permission:<module>:<feature>:<action>`) | Description |
|---|---|---|---|---|
| **Requisitions** | `requisition` | `create` | `permission:requisitions:requisition:create` | Create new job requisition requests |
| | | `read` | `permission:requisitions:requisition:read` | View job requisitions and approval status |
| | | `update` | `permission:requisitions:requisition:update` | Edit requisition details, budget, or headcount |
| | | `delete` | `permission:requisitions:requisition:delete` | Delete draft or cancelled requisitions |
| | | `approve` | `permission:requisitions:requisition:approve` | Execute approval or rejection decisions |
| | | `cancel` | `permission:requisitions:requisition:cancel` | Cancel an active requisition workflow |
| **Postings** | `posting` | `create` | `permission:postings:posting:create` | Create job postings / JD drafts |
| | | `read` | `permission:postings:posting:read` | View internal job postings and status |
| | | `update` | `permission:postings:posting:update` | Edit job posting content and channels |
| | | `delete` | `permission:postings:posting:delete` | Remove/delete job postings |
| | | `publish` | `permission:postings:posting:publish` | Publish job postings to public board/channels |
| | | `cancel` | `permission:postings:posting:cancel` | Unpublish or close job postings |
| **Applications** | `application` | `create` | `permission:applications:application:create` | Create/ingest candidate applications |
| | | `read` | `permission:applications:application:read` | View job applications, resumes, candidate details |
| | | `update` | `permission:applications:application:update` | Move application stage, add tags, update status |
| | | `delete` | `permission:applications:application:delete` | Delete job application records |
| **Interviews** | `interview` | `create` | `permission:interviews:interview:create` | Create interview session definitions |
| | | `read` | `permission:interviews:interview:read` | View scheduled interviews and panel details |
| | | `update` | `permission:interviews:interview:update` | Edit interview details or reschedule |
| | | `delete` | `permission:interviews:interview:delete` | Delete interview session records |
| | | `schedule` | `permission:interviews:interview:schedule` | Schedule candidate interview slots with panel |
| | | `cancel` | `permission:interviews:interview:cancel` | Cancel scheduled interview sessions |
| **Scorecards** | `scorecard` | `create` | `permission:scorecards:scorecard:create` | Create scorecard templates / evaluations |
| | | `read` | `permission:scorecards:scorecard:read` | View evaluation scorecards and feedback |
| | | `update` | `permission:scorecards:scorecard:update` | Edit scorecard templates or draft feedback |
| | | `delete` | `permission:scorecards:scorecard:delete` | Delete scorecard records |
| | | `submit` | `permission:scorecards:scorecard:submit` | Submit completed candidate evaluation scorecard |
| | | `blindevaluation` | `permission:scorecards:scorecard:blindevaluation` | Conduct blind evaluation (masked candidate PII) |
| **Users** | `user` | `create` | `permission:users:user:create` | Invite / create new system users |
| | | `read` | `permission:users:user:read` | View user list and user profile details |
| | | `update` | `permission:users:user:update` | Edit user profile and status (active/inactive) |
| | | `delete` | `permission:users:user:delete` | Remove or deactivate system users |
| | | `assign` | `permission:users:user:assign` | Assign roles and department access to users |
| **Roles** | `role` | `create` | `permission:roles:role:create` | Create new custom system/tenant roles |
| | | `read` | `permission:roles:role:read` | View role definitions and permission matrices |
| | | `update` | `permission:roles:role:update` | Modify role permissions and role metadata |
| | | `delete` | `permission:roles:role:delete` | Delete custom roles |
| **Settings** | `setting` | `create` | `permission:settings:setting:create` | Create system/tenant configuration settings |
| | | `read` | `permission:settings:setting:read` | View system/tenant settings and integrations |
| | | `update` | `permission:settings:setting:update` | Update configuration settings and integrations |
| | | `delete` | `permission:settings:setting:delete` | Delete system/tenant settings |
| **System** *(SuperAdmin)* | `tenant` | `manage` | `permission:system:tenant:manage` | Cross-tenant administration & tenant switching |

---

## 3. Pre-Configured Role-Permission Seed Matrix

### 3.1 Role Descriptions & Scoping Rules
1. **`SuperAdmin`**: Global system administrator. Holds ALL permission codes across all modules, including `permission:system:tenant:manage` for cross-tenant provisioning and management.
2. **`Admin`**: Tenant system administrator. Holds full tenant-level CRUD and administration permissions across all 8 standard modules.
3. **`HrDirector`**: HR Leader / Executive. Manages high-level recruitment operations: Requisitions (CRUD, Approve), Postings (CRUD, Publish), Applications, Interviews, Scorecards, and Users (Read, Assign).
4. **`Recruiter`**: In-house talent acquisition staff. Manages day-to-day pipeline: Requisitions (Create, Read), Postings (CRUD, Publish), Applications (CRUD), Interviews (Read, Schedule, Cancel), Scorecards (Create, Read, Submit, BlindEvaluation).
5. **`HiringManager`**: Department manager. Department-scoped access: Requisitions (Create, Update/Edit), Applications (Read), Interviews (Read, Schedule), Scorecards (Read, Submit, BlindEvaluation).
6. **`Approver`**: Requisition approval chain participant (Finance/Dept Head). Requisitions (Read, Approve), Applications (Read).
7. **`Interviewer`**: Interview panel member. Focuses exclusively on assigned candidates: Interviews (Read), Scorecards (Read, Submit, BlindEvaluation).

### 3.2 Pre-Configured Permission Matrix Table

The following matrix defines the exact initial permission mapping for each system role:

| Permission Code | SuperAdmin | Admin | HrDirector | Recruiter | HiringManager | Approver | Interviewer |
|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `permission:requisitions:requisition:create` | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ |
| `permission:requisitions:requisition:read` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ |
| `permission:requisitions:requisition:update` | ✅ | ✅ | ✅ | ❌ | ✅ | ❌ | ❌ |
| `permission:requisitions:requisition:delete` | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ |
| `permission:requisitions:requisition:approve` | ✅ | ✅ | ✅ | ❌ | ❌ | ✅ | ❌ |
| `permission:requisitions:requisition:cancel` | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ |
| `permission:postings:posting:create` | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ |
| `permission:postings:posting:read` | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ |
| `permission:postings:posting:update` | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ |
| `permission:postings:posting:delete` | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ |
| `permission:postings:posting:publish` | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ |
| `permission:postings:posting:cancel` | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ |
| `permission:applications:application:create` | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ |
| `permission:applications:application:read` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ |
| `permission:applications:application:update` | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ |
| `permission:applications:application:delete` | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ |
| `permission:interviews:interview:create` | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ |
| `permission:interviews:interview:read` | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ | ✅ |
| `permission:interviews:interview:update` | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ |
| `permission:interviews:interview:delete` | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ |
| `permission:interviews:interview:schedule` | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ |
| `permission:interviews:interview:cancel` | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ |
| `permission:scorecards:scorecard:create` | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ |
| `permission:scorecards:scorecard:read` | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ | ✅ |
| `permission:scorecards:scorecard:update` | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ |
| `permission:scorecards:scorecard:delete` | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ |
| `permission:scorecards:scorecard:submit` | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ | ✅ |
| `permission:scorecards:scorecard:blindevaluation` | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ | ✅ |
| `permission:users:user:create` | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ |
| `permission:users:user:read` | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ |
| `permission:users:user:update` | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ |
| `permission:users:user:delete` | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ |
| `permission:users:user:assign` | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ |
| `permission:roles:role:create` | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ |
| `permission:roles:role:read` | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ |
| `permission:roles:role:update` | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ |
| `permission:roles:role:delete` | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ |
| `permission:settings:setting:create` | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ |
| `permission:settings:setting:read` | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ |
| `permission:settings:setting:update` | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ |
| `permission:settings:setting:delete` | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ |
| `permission:system:tenant:manage` | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |

---

## 4. EF Core Data Model & Seed Strategy

### 4.1 Target Entity Definitions in `Domain/Entities`

To support dynamic RBAC, the following domain entities will be created in `RecruitOps.Domain.Entities`:

```csharp
namespace RecruitOps.Domain.Entities;

public class Permission : BaseEntity
{
    public string Code { get; set; } = string.Empty; // e.g., permission:requisitions:requisition:create
    public string Module { get; set; } = string.Empty;
    public string Feature { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsSystem { get; set; } = true;

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}

public class Role : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty; // e.g., Recruiter, HrDirector
    public string Description { get; set; } = string.Empty;
    public bool IsSystemRole { get; set; } = false;
    public bool IsSuperAdmin { get; set; } = false;

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    public ICollection<User> Users { get; set; } = new List<User>();
}

public class RolePermission
{
    public Guid RoleId { get; set; }
    public Role Role { get; set; } = null!;

    public Guid PermissionId { get; set; }
    public Permission Permission { get; set; } = null!;
}
```

### 4.2 Deterministic GUID Seeding Strategy

To enable seamless EF Core migrations and idempotent runtime seeding, static deterministic GUIDs are assigned to each system permission and system role:

* **Permission Namespace Deterministic Base**: `Guid.Parse("10000000-0000-0000-0000-0000000000XX")`
* **System Role Deterministic Base**: `Guid.Parse("20000000-0000-0000-0000-0000000000YY")`

#### Sample Deterministic Mapping:
* `SuperAdmin` Role ID: `20000000-0000-0000-0000-000000000001`
* `Admin` Role ID: `20000000-0000-0000-0000-000000000002`
* `HrDirector` Role ID: `20000000-0000-0000-0000-000000000003`
* `Recruiter` Role ID: `20000000-0000-0000-0000-000000000004`
* `HiringManager` Role ID: `20000000-0000-0000-0000-000000000005`
* `Approver` Role ID: `20000000-0000-0000-0000-000000000006`
* `Interviewer` Role ID: `20000000-0000-0000-0000-000000000007`

### 4.3 Idempotent Runtime Initialization Blueprint (`DbInitializer.cs`)

While EF Core `HasData()` can be used in migrations, runtime seeding in `DbInitializer.SeedAsync()` guarantees that whenever new features or permissions are introduced in future builds, existing tenant databases automatically sync the permission table and default role assignments upon startup.

```csharp
public static async Task SeedPermissionsAndRolesAsync(AppDbContext db, Guid tenantId, CancellationToken ct)
{
    // 1. Ensure all system permissions exist
    var existingCodes = await db.Permissions.Select(p => p.Code).ToListAsync(ct);
    var missingPermissions = SystemPermissions.All
        .Where(p => !existingCodes.Contains(p.Code))
        .ToList();

    if (missingPermissions.Any())
    {
        await db.Permissions.AddRangeAsync(missingPermissions, ct);
        await db.SaveChangesAsync(ct);
    }

    // 2. Ensure system default roles exist for tenant
    var permissionsMap = await db.Permissions.ToDictionaryAsync(p => p.Code, ct);

    foreach (var defaultRoleDef in DefaultRoleDefinitions.All)
    {
        var role = await db.Roles
            .Include(r => r.RolePermissions)
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Name == defaultRoleDef.Name, ct);

        if (role == null)
        {
            role = new Role
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = defaultRoleDef.Name,
                Description = defaultRoleDef.Description,
                IsSystemRole = true,
                IsSuperAdmin = defaultRoleDef.IsSuperAdmin
            };
            db.Roles.Add(role);
            await db.SaveChangesAsync(ct);
        }

        // Sync role permissions
        var existingPermIds = role.RolePermissions.Select(rp => rp.PermissionId).ToHashSet();
        foreach (var permCode in defaultRoleDef.PermissionCodes)
        {
            if (permissionsMap.TryGetValue(permCode, out var perm) && !existingPermIds.Contains(perm.Id))
            {
                db.RolePermissions.Add(new RolePermission
                {
                    RoleId = role.Id,
                    PermissionId = perm.Id
                });
            }
        }
    }
    await db.SaveChangesAsync(ct);
}
```

---

## 5. Backwards Compatibility & Migration Strategy

1. **User Table Migration**: `User` entity gains `RoleId` (nullable Guid during migration, non-nullable after seed).
2. **Data Migration Script**: EF Core migration script automatically assigns existing users with `UserRole.Admin` to the seeded `Admin` `Role` record, `UserRole.Recruiter` to `Recruiter`, etc.
3. **JWT Claim Mapping**: `JwtTokenService` includes both `role` claim (for legacy policies) and `permission` claims array in issued JWT tokens.

---

## 6. Conclusion

The designed taxonomy and seed matrix provide a robust, scalable foundation for Milestone 2. It accommodates standard CRUD, specialized workflow actions, cross-tenant management for `SuperAdmin`, and department-level data boundary enforcement without requiring structural breaking changes.
