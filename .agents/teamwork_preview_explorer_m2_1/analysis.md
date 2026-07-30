# Requirement R2: Granular Dynamic RBAC Data Model & Super-Admin Design Analysis

## Executive Summary
This document presents the detailed architectural design and domain model specifications for **Requirement R2: Granular Dynamic RBAC Data Model & Super-Admin** in RecruitOps.

The design introduces dynamic, configurable Roles and granular Permissions while maintaining 100% backwards compatibility with the existing `UserRole` enum system and multi-tenant database isolation.

---

## 1. Baseline System Inspection

### 1.1 Existing Domain & Persistence Structure
1. **`User.cs` (`src/Domain/Entities/User.cs`)**:
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
2. **`UserRole.cs` (`src/Domain/Enums/UserRole.cs`)**:
   ```csharp
   public enum UserRole
   {
       Admin,
       HrDirector,
       Recruiter,
       HiringManager,
       Approver,
   }
   ```
3. **`AppDbContext.cs` (`src/Infrastructure/Persistence/AppDbContext.cs`)**:
   - `User.Role` is configured as a string column:
     `builder.Entity<User>().Property(x => x.Role).HasConversion<string>().HasMaxLength(30);`
   - Tenant query filter is applied globally to `User`:
     `builder.Entity<User>().HasQueryFilter(e => e.TenantId == _tenant.TenantId);`
   - Auto-stamping in `StampTenantAndTimestamps()` sets `TenantId = _tenant.TenantId` for newly added `ITenantScoped` entities when `TenantId == Guid.Empty`.
4. **Authorization Mechanics (`RoleScope.cs` & `CurrentUser.cs`)**:
   - `RoleScope.IsDepartmentScoped(UserRole role)`: Checks if role is `HiringManager`.
   - `RoleScope.IsExcludedFromCandidateData(UserRole role)`: Checks if role is `Approver`.
   - `CurrentUser` parses claim `ClaimTypes.Role` to `UserRole` enum via `RoleScope.Parse(Role)`.

---

## 2. Proposed Domain Entities Design

To support dynamic RBAC, three new entities are introduced, alongside modifications to `User.cs`.

### 2.1 Entity `Role.cs`
- **Location**: `backend/src/Domain/Entities/Role.cs`
- **Purpose**: Defines custom or system-seeded roles with permission bindings.
- **Properties**:
  - `Guid? TenantId` (Nullable): `null` for system-wide roles (Super-Admin and standard system roles), or `<TenantGuid>` for custom tenant-scoped roles.
  - `string Name`: Human-readable role display name (e.g. "Recruiter", "Finance Manager").
  - `string Code`: Unique machine-readable identifier (e.g. `super_admin`, `admin`, `recruiter`, `custom_approver`).
  - `string Description`: Detailed description of role scope and capabilities.
  - `bool IsSystemRole`: `true` for system-predefined immutable roles; `false` for tenant-created custom roles.
  - `bool IsSuperAdmin`: `true` if this role grants cross-tenant system administration access.
  - `bool IsActive`: Active status flag (`true` by default).

```csharp
using RecruitOps.Domain.Common;

namespace RecruitOps.Domain.Entities;

/// <summary>Represents a system-defined or custom tenant-defined RBAC role.</summary>
public class Role : BaseEntity
{
    /// <summary>Null for system-wide roles (global/super-admin/templates); populated for tenant custom roles.</summary>
    public Guid? TenantId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Unique canonical identifier e.g. "super_admin", "admin", "recruiter", "custom_finance".</summary>
    public string Code { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>System roles cannot be renamed or deleted by tenant administrators.</summary>
    public bool IsSystemRole { get; set; }

    /// <summary>Grants cross-tenant Super-Admin administrative privileges.</summary>
    public bool IsSuperAdmin { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation properties
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    public ICollection<User> Users { get; set; } = new List<User>();
}
```

---

### 2.2 Entity `Permission.cs`
- **Location**: `backend/src/Domain/Entities/Permission.cs`
- **Purpose**: Granular permission definition for features and actions across modules.
- **Properties**:
  - `string Module`: Functional module boundary (e.g. `Requisitions`, `Candidates`, `Interviews`, `System`, `Settings`).
  - `string Feature`: Specific component/feature (e.g. `Approval`, `Pipeline`, `Scorecards`, `Users`, `Tenants`).
  - `string Action`: Action operation (e.g. `Read`, `Create`, `Update`, `Delete`, `Approve`, `Manage`).
  - `string Name`: Display name (e.g. "Approve Requisition").
  - `string Description`: Detailed permission guidance.
  - `string Code`: Canonical string representation using standard colon syntax: `permission:<module>:<feature>:<action>` (e.g. `permission:requisitions:approval:approve`, `permission:candidates:pipeline:read`, `permission:system:tenants:manage`).

```csharp
using RecruitOps.Domain.Common;

namespace RecruitOps.Domain.Entities;

/// <summary>Granular permission definition for system authorization.</summary>
public class Permission : BaseEntity
{
    public string Module { get; set; } = string.Empty;
    public string Feature { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>Canonical permission string e.g. permission:requisitions:approval:approve</summary>
    public string Code { get; set; } = string.Empty;

    // Navigation properties
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
```

---

### 2.3 Entity `RolePermission.cs`
- **Location**: `backend/src/Domain/Entities/RolePermission.cs`
- **Purpose**: Join entity linking `Role` and `Permission`.

```csharp
namespace RecruitOps.Domain.Entities;

/// <summary>Many-to-many relationship join entity between Role and Permission.</summary>
public class RolePermission
{
    public Guid RoleId { get; set; }
    public Role Role { get; set; } = null!;

    public Guid PermissionId { get; set; }
    public Permission Permission { get; set; } = null!;

    public DateTimeOffset AssignedAt { get; set; } = DateTimeOffset.UtcNow;
}
```

---

### 2.4 Updated `User.cs` & Backwards Compatibility
- **Location**: `backend/src/Domain/Entities/User.cs`
- **Modifications**:
  - Add `Guid? RoleId` foreign key referencing `Role`.
  - Add `Role? CustomRole` navigation property.
  - Add `bool IsSuperAdmin` flag on `User` entity for fast-path Super-Admin checks.
  - Retain `public UserRole Role { get; set; }` enum property with a backward-compatible mapping getter/setter.

```csharp
using RecruitOps.Domain.Common;
using RecruitOps.Domain.Enums;

namespace RecruitOps.Domain.Entities;

/// <summary>A staff member of an agency. RBAC via <see cref="Role"/> and legacy <see cref="UserRole"/>.</summary>
public class User : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Hashed with IPasswordHasher — never store plaintext.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Legacy enum property for backwards compatibility across existing controllers & services.</summary>
    public UserRole Role { get; set; } = UserRole.Recruiter;

    /// <summary>Foreign key to dynamic Role entity.</summary>
    public Guid? RoleId { get; set; }
    public Role? CustomRole { get; set; }

    /// <summary>Direct flag indicating if the user has system Super-Admin cross-tenant capabilities.</summary>
    public bool IsSuperAdmin { get; set; }

    public bool IsActive { get; set; } = true;
}
```

---

## 3. Super-Admin & Multi-Tenant Cross-Access Design

### 3.1 Super-Admin Definition
1. **System Super-Admin Role**:
   - `Role.Code` = `"super_admin"`
   - `Role.TenantId` = `null`
   - `Role.IsSystemRole` = `true`
   - `Role.IsSuperAdmin` = `true`
   - Possesses wildcard / system management permissions (`permission:system:tenants:manage`, `permission:system:settings:manage`, `permission:system:audit:read`).

2. **Super-Admin User Representation**:
   - `User.IsSuperAdmin` boolean property set to `true`.
   - `User.RoleId` linked to the system `super_admin` Role ID.
   - `User.TenantId` can be assigned to a designated System Tenant ID (e.g. `Guid.Empty` or dedicated System Tenant ID).

3. **EF Core Query Filter Handling for System Roles & Super-Admin**:
   - **System Roles Query Filter**: System roles (`TenantId == null`) must be readable across all tenants. Therefore, the EF Core query filter for `Role` is configured as:
     ```csharp
     builder.Entity<Role>().HasQueryFilter(e => e.TenantId == null || e.TenantId == _tenant.TenantId);
     ```
   - **Cross-Tenant Super-Admin Queries**: When a Super-Admin user performs global system administration tasks (e.g., viewing/managing all tenants or users across tenants), the application service explicitly applies `.IgnoreQueryFilters()` on tenant-scoped queries.

---

## 4. EF Core Configuration & Database Schema Specs

### 4.1 Configurations in `AppDbContext.cs` (`OnModelCreating`)

```csharp
// ---------- Role ----------
builder.Entity<Role>(e =>
{
    e.Property(x => x.Name).IsRequired().HasMaxLength(100);
    e.Property(x => x.Code).IsRequired().HasMaxLength(50);
    e.Property(x => x.Description).HasMaxLength(500);
    
    // Unique index: system roles (TenantId == null) must have unique Code.
    // Custom roles per tenant (TenantId != null) must have unique (TenantId, Code).
    e.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
});

// ---------- Permission ----------
builder.Entity<Permission>(e =>
{
    e.Property(x => x.Module).IsRequired().HasMaxLength(50);
    e.Property(x => x.Feature).IsRequired().HasMaxLength(50);
    e.Property(x => x.Action).IsRequired().HasMaxLength(50);
    e.Property(x => x.Name).IsRequired().HasMaxLength(100);
    e.Property(x => x.Description).HasMaxLength(500);
    e.Property(x => x.Code).IsRequired().HasMaxLength(100);
    
    // Unique canonical permission code index
    e.HasIndex(x => x.Code).IsUnique();
});

// ---------- RolePermission ----------
builder.Entity<RolePermission>(e =>
{
    e.HasKey(x => new { x.RoleId, x.PermissionId });

    e.HasOne(x => x.Role)
        .WithMany(r => r.RolePermissions)
        .HasForeignKey(x => x.RoleId)
        .OnDelete(DeleteBehavior.Cascade);

    e.HasOne(x => x.Permission)
        .WithMany(p => p.RolePermissions)
        .HasForeignKey(x => x.PermissionId)
        .OnDelete(DeleteBehavior.Cascade);
});

// ---------- User Updates ----------
builder.Entity<User>(e =>
{
    // Existing configuration...
    e.HasOne(x => x.CustomRole)
        .WithMany(r => r.Users)
        .HasForeignKey(x => x.RoleId)
        .OnDelete(DeleteBehavior.Restrict);
});

// ---------- Query Filters ----------
builder.Entity<Role>().HasQueryFilter(e => e.TenantId == null || e.TenantId == _tenant.TenantId);
builder.Entity<RolePermission>().HasQueryFilter(e => e.Role.TenantId == null || e.Role.TenantId == _tenant.TenantId);
```

---

## 5. System Role & Permission Seeding Strategy

In `DbInitializer.cs`, standard permissions and default system roles are automatically seeded:

1. **Standard Permissions**:
   - `permission:requisitions:requisition:create`
   - `permission:requisitions:requisition:read`
   - `permission:requisitions:requisition:update`
   - `permission:requisitions:approval:approve`
   - `permission:candidates:pipeline:read`
   - `permission:candidates:pipeline:write`
   - `permission:interviews:schedule:write`
   - `permission:scorecards:evaluate:write`
   - `permission:system:tenants:manage`
   - `permission:system:rbac:manage`

2. **Standard System Roles Seeded**:
   - `super_admin`: Full system permission set + `IsSuperAdmin = true`.
   - `admin`: All operational permissions within tenant.
   - `hr_director`: All reports, requisitions, budgets, approvals.
   - `recruiter`: Full pipeline access (posting, candidates, interviews).
   - `hiring_manager`: Department requisition creation, candidate evaluation.
   - `approver`: Requisition approval sign-off.

3. **User Migration / Default Association**:
   - Existing users with `UserRole.Admin` have their `RoleId` populated with the seeded `admin` System Role ID.
   - Existing users with `UserRole.Recruiter` have their `RoleId` populated with the seeded `recruiter` System Role ID, etc.

---

## 6. Backwards Compatibility Verification Plan

1. **Enum Compatibility**: `User.Role` property remains functional for all existing code calling `user.Role == UserRole.Admin`.
2. **JWT Claims**: `JwtTokenService` continues outputting `ClaimTypes.Role = user.Role.ToString()` alongside new permission claims (`permission` array in JWT).
3. **RoleScope & CurrentUser**: `RoleScope.IsDepartmentScoped` and `RoleScope.IsExcludedFromCandidateData` continue operating seamlessly without breaking changes.
4. **Integration Test Suite**: All existing 172 unit and integration tests continue to pass without modification.
