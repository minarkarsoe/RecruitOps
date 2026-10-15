# Technical Design Report: EF Core Data Model, Configurations, Seeding & Migration Plan for Requirement R2 (Dynamic RBAC)

**Project:** RecruitOps  
**Milestone:** 2 (Granular Dynamic RBAC Data Model & Migration)  
**Author:** Explorer 3 (`teamwork_preview_explorer_m2_3`)  
**Date:** 2026-07-29  

---

## 1. Executive Summary

Requirement R2 transitions RecruitOps from a static enum-based role system (`UserRole`) to a **Granular Dynamic Role-Based Access Control (RBAC)** architecture. This design delivers:
1. **Domain Entities**: `Role`, `Permission`, and `RolePermission` join entity, while maintaining backwards compatibility on `User`.
2. **EF Core Fluent API Configurations**: Strict constraints, unique composite indexes on `(TenantId, Code)`, `Code`, and `(RoleId, PermissionId)`, explicit cascade vs. restrict delete behaviors, and global tenant query filters.
3. **Automated Seeding Framework**: `RbacSeedData.cs` static permission taxonomy & default role matrix, combined with idempotent `AppDbContextSeed.cs` execution on application startup.
4. **Migration Strategy**: Backward-compatible EF Core schema migration (`Module2DynamicRbac`) with backfilling of legacy `User.Role` enum mappings to new `Role` entity relations.

---

## 2. Domain Entity Specifications

### 2.1 `Role.cs` (`Domain/Entities/Role.cs`)
Represents a user role within a tenant. Supports system default roles as well as custom user-defined roles.

```csharp
using RecruitOps.Domain.Common;

namespace RecruitOps.Domain.Entities;

/// <summary>
/// Domain entity representing a tenant-scoped or system role.
/// </summary>
public class Role : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    
    /// <summary>Human-readable display name (e.g., "HR Director", "Recruiter").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Standardized role code (e.g., "admin", "hr_director", "recruiter", "custom_recruiter").</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Description of the role's scope and responsibilities.</summary>
    public string? Description { get; set; }

    /// <summary>True for pre-packaged system roles (Admin, HrDirector, Recruiter, etc.) that cannot be deleted.</summary>
    public bool IsSystemRole { get; set; } = false;

    /// <summary>True for SuperAdmin role which bypasses tenant isolation checks.</summary>
    public bool IsSuperAdmin { get; set; } = false;

    // Navigation Properties
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    public ICollection<User> Users { get; set; } = new List<User>();
}
```

### 2.2 `Permission.cs` (`Domain/Entities/Permission.cs`)
Represents a single granular capability definition in the global system taxonomy.

```csharp
using RecruitOps.Domain.Common;

namespace RecruitOps.Domain.Entities;

/// <summary>
/// Global permission definition formatted as permission:module:feature:action.
/// </summary>
public class Permission : BaseEntity
{
    /// <summary>
    /// Unique permission string code (e.g., "permission:requisitions:requisitions:create").
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Functional module (e.g., "Requisitions", "Postings", "Applications", "Interviews", "Scorecards", "Users", "Roles", "Settings").</summary>
    public string Module { get; set; } = string.Empty;

    /// <summary>Feature component (e.g., "Requisitions", "Scorecards").</summary>
    public string Feature { get; set; } = string.Empty;

    /// <summary>Action capability (e.g., "Create", "Read", "Update", "Delete", "Approve", "Publish", "Cancel", "BlindEvaluation").</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Human-readable description of what this permission allows.</summary>
    public string? Description { get; set; }

    // Navigation Properties
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
```

### 2.3 `RolePermission.cs` (`Domain/Entities/RolePermission.cs`)
Join entity mapping a `Role` to a `Permission` for a given tenant.

```csharp
using RecruitOps.Domain.Common;

namespace RecruitOps.Domain.Entities;

/// <summary>
/// Many-to-many join entity mapping Role to Permission.
/// </summary>
public class RolePermission : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid RoleId { get; set; }
    public Role Role { get; set; } = null!;

    public Guid PermissionId { get; set; }
    public Permission Permission { get; set; } = null!;
}
```

### 2.4 `User.cs` Updates (`Domain/Entities/User.cs`)
Enhance `User` entity to include a foreign key and navigation property to `Role`, preserving `UserRole` enum as a fallback for backwards compatibility.

```csharp
using RecruitOps.Domain.Common;
using RecruitOps.Domain.Enums;

namespace RecruitOps.Domain.Entities;

/// <summary>A staff member of an agency. Dynamic RBAC via <see cref="RoleId"/> and <see cref="RoleEntity"/>.</summary>
public class User : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Hashed with IPasswordHasher — never store plaintext.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Dynamic RBAC role foreign key (Requirement R2).</summary>
    public Guid? RoleId { get; set; }
    public Role? RoleEntity { get; set; }

    /// <summary>Legacy enum role retained for fallback & backward compatibility.</summary>
    public UserRole Role { get; set; } = UserRole.Recruiter;

    public bool IsActive { get; set; } = true;
}
```

---

## 3. EF Core Fluent API Configurations & DbContext Updates

### 3.1 Persistence Configuration Classes

#### 3.1.1 `RoleConfiguration.cs` (`Infrastructure/Persistence/Configurations/RoleConfiguration.cs`)
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RecruitOps.Domain.Entities;

namespace RecruitOps.Infrastructure.Persistence.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Description)
            .HasMaxLength(500);

        builder.Property(x => x.IsSystemRole)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.IsSuperAdmin)
            .IsRequired()
            .HasDefaultValue(false);

        // Unique constraint per tenant: Role Code must be unique per tenant
        builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        
        // Non-unique index for fast lookup by tenant and name
        builder.HasIndex(x => new { x.TenantId, x.Name });

        // Delete behavior: deleting a role cascades to RolePermissions
        builder.HasMany(r => r.RolePermissions)
            .WithOne(rp => rp.Role)
            .HasForeignKey(rp => rp.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        // Delete behavior: deleting a role assigned to active users IS BLOCKED
        builder.HasMany(r => r.Users)
            .WithOne(u => u.RoleEntity)
            .HasForeignKey(u => u.RoleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

#### 3.1.2 `PermissionConfiguration.cs` (`Infrastructure/Persistence/Configurations/PermissionConfiguration.cs`)
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RecruitOps.Domain.Entities;

namespace RecruitOps.Infrastructure.Persistence.Configurations;

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("Permissions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Module)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Feature)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Action)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Description)
            .HasMaxLength(500);

        // System-wide unique permission code
        builder.HasIndex(x => x.Code).IsUnique();

        // Index for filtering UI permission assignment grid by module & feature
        builder.HasIndex(x => new { x.Module, x.Feature });

        // Delete behavior: deleting a permission cascades to RolePermissions
        builder.HasMany(p => p.RolePermissions)
            .WithOne(rp => rp.Permission)
            .HasForeignKey(rp => rp.PermissionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

#### 3.1.3 `RolePermissionConfiguration.cs` (`Infrastructure/Persistence/Configurations/RolePermissionConfiguration.cs`)
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RecruitOps.Domain.Entities;

namespace RecruitOps.Infrastructure.Persistence.Configurations;

public class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("RolePermissions");

        builder.HasKey(x => x.Id);

        // Prevent assigning the same permission multiple times to a role
        builder.HasIndex(x => new { x.RoleId, x.PermissionId }).IsUnique();

        // Fast lookup index for resolving role permissions by tenant
        builder.HasIndex(x => new { x.TenantId, x.RoleId });

        builder.HasOne(x => x.Role)
            .WithMany(r => r.RolePermissions)
            .HasForeignKey(x => x.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Permission)
            .WithMany(p => p.RolePermissions)
            .HasForeignKey(x => x.PermissionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

#### 3.1.4 `UserConfiguration.cs` (`Infrastructure/Persistence/Configurations/UserConfiguration.cs`)
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RecruitOps.Domain.Entities;

namespace RecruitOps.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(x => x.Email).IsRequired().HasMaxLength(256);
        builder.Property(x => x.DisplayName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.PasswordHash).IsRequired().HasMaxLength(500);
        builder.Property(x => x.Role).HasConversion<string>().HasMaxLength(30);

        builder.HasIndex(x => x.Email).IsUnique();

        // Index on RoleId FK for join performance
        builder.HasIndex(x => x.RoleId);

        // Restrict role deletion if user is assigned
        builder.HasOne(x => x.RoleEntity)
            .WithMany(r => r.Users)
            .HasForeignKey(x => x.RoleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

---

### 3.2 `AppDbContext.cs` Registration & Query Filter Updates

Add new `DbSet` properties to `AppDbContext`:

```csharp
public DbSet<Role> Roles => Set<Role>();
public DbSet<Permission> Permissions => Set<Permission>();
public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
```

In `OnModelCreating(ModelBuilder builder)`:

```csharp
// Apply entity configurations from assembly
builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

// Add Global Tenant Query Filters for R2 entities
builder.Entity<Role>().HasQueryFilter(e => e.TenantId == _tenant.TenantId);
builder.Entity<RolePermission>().HasQueryFilter(e => e.TenantId == _tenant.TenantId);
```

---

## 4. RBAC Seeding Data Framework

### 4.1 Taxonomy Definition (`RbacSeedData.cs`)

Create `Infrastructure/Persistence/Seed/RbacSeedData.cs` containing full static taxonomy and default role matrix definitions:

```csharp
namespace RecruitOps.Infrastructure.Persistence.Seed;

public static class RbacSeedData
{
    public static class SystemRoleCodes
    {
        public const string SuperAdmin = "super_admin";
        public const string Admin = "admin";
        public const string HrDirector = "hr_director";
        public const string Recruiter = "recruiter";
        public const string HiringManager = "hiring_manager";
        public const string Approver = "approver";
        public const string Interviewer = "interviewer";
    }

    public record PermissionSeedItem(string Code, string Module, string Feature, string Action, string Description);

    public static readonly List<PermissionSeedItem> Permissions = new()
    {
        // Requisitions
        new("permission:requisitions:requisitions:create", "Requisitions", "Requisitions", "Create", "Create job requisitions"),
        new("permission:requisitions:requisitions:read", "Requisitions", "Requisitions", "Read", "View job requisitions"),
        new("permission:requisitions:requisitions:update", "Requisitions", "Requisitions", "Update", "Update draft job requisitions"),
        new("permission:requisitions:requisitions:delete", "Requisitions", "Requisitions", "Delete", "Delete draft job requisitions"),
        new("permission:requisitions:requisitions:approve", "Requisitions", "Requisitions", "Approve", "Approve or reject job requisitions"),

        // Job Postings
        new("permission:postings:postings:create", "Postings", "Postings", "Create", "Create job postings from approved requisitions"),
        new("permission:postings:postings:read", "Postings", "Postings", "Read", "View job postings"),
        new("permission:postings:postings:update", "Postings", "Postings", "Update", "Edit job posting content and settings"),
        new("permission:postings:postings:delete", "Postings", "Postings", "Delete", "Delete job postings"),
        new("permission:postings:postings:publish", "Postings", "Postings", "Publish", "Publish or unpublish job postings to portal"),

        // Candidates & Applications
        new("permission:applications:applications:create", "Applications", "Applications", "Create", "Create job applications"),
        new("permission:applications:applications:read", "Applications", "Applications", "Read", "View candidate applications"),
        new("permission:applications:applications:update", "Applications", "Applications", "Update", "Update job application status"),
        new("permission:applications:applications:delete", "Applications", "Applications", "Delete", "Delete applications"),

        // Interviews
        new("permission:interviews:interviews:read", "Interviews", "Interviews", "Read", "View scheduled interviews"),
        new("permission:interviews:interviews:schedule", "Interviews", "Interviews", "Schedule", "Schedule candidate interview rounds"),
        new("permission:interviews:interviews:cancel", "Interviews", "Interviews", "Cancel", "Cancel scheduled interviews"),

        // Scorecards
        new("permission:scorecards:scorecards:read", "Scorecards", "Scorecards", "Read", "View interview scorecards"),
        new("permission:scorecards:scorecards:submit", "Scorecards", "Scorecards", "Submit", "Submit interview scorecards"),
        new("permission:scorecards:scorecards:blind_evaluation", "Scorecards", "Scorecards", "BlindEvaluation", "Participate in blind scorecard evaluation"),

        // User Directory
        new("permission:users:users:read", "Users", "Users", "Read", "View directory users"),
        new("permission:users:users:assign", "Users", "Users", "Assign", "Assign user roles"),
        new("permission:users:users:create", "Users", "Users", "Create", "Create new system users"),
        new("permission:users:users:update", "Users", "Users", "Update", "Update user profile details"),

        // Dynamic Roles
        new("permission:roles:roles:read", "Roles", "Roles", "Read", "View dynamic roles and permissions"),
        new("permission:roles:roles:create", "Roles", "Roles", "Create", "Create custom system roles"),
        new("permission:roles:roles:update", "Roles", "Roles", "Update", "Edit custom system roles"),
        new("permission:roles:roles:delete", "Roles", "Roles", "Delete", "Delete custom system roles"),

        // Settings
        new("permission:settings:settings:read", "Settings", "Settings", "Read", "View system settings"),
        new("permission:settings:settings:update", "Settings", "Settings", "Update", "Update system settings"),
    };
}
```

---

### 4.2 Seed Logic Implementation (`AppDbContextSeed.cs`)

Create `Infrastructure/Persistence/Seed/AppDbContextSeed.cs` to execute seed operations idempotently:

```csharp
using Microsoft.EntityFrameworkCore;
using RecruitOps.Domain.Entities;
using RecruitOps.Domain.Enums;

namespace RecruitOps.Infrastructure.Persistence.Seed;

public static class AppDbContextSeed
{
    public static async Task SeedRbacAsync(AppDbContext db, Guid tenantId, CancellationToken ct = default)
    {
        // 1. Seed / Upsert Permissions Catalog (Global)
        var existingPerms = await db.Permissions.ToDictionaryAsync(p => p.Code, ct);
        foreach (var pSeed in RbacSeedData.Permissions)
        {
            if (!existingPerms.TryGetValue(pSeed.Code, out var perm))
            {
                perm = new Permission
                {
                    Code = pSeed.Code,
                    Module = pSeed.Module,
                    Feature = pSeed.Feature,
                    Action = pSeed.Action,
                    Description = pSeed.Description
                };
                db.Permissions.Add(perm);
                existingPerms[pSeed.Code] = perm;
            }
        }
        await db.SaveChangesAsync(ct);

        // 2. Seed Default System Roles for Tenant
        var existingRoles = await db.Roles
            .IgnoreQueryFilters()
            .Where(r => r.TenantId == tenantId)
            .ToDictionaryAsync(r => r.Code, ct);

        var systemRoleDefs = GetSystemRoleDefinitions();
        foreach (var def in systemRoleDefs)
        {
            if (!existingRoles.TryGetValue(def.Code, out var role))
            {
                role = new Role
                {
                    TenantId = tenantId,
                    Code = def.Code,
                    Name = def.Name,
                    Description = def.Description,
                    IsSystemRole = true,
                    IsSuperAdmin = def.IsSuperAdmin
                };
                db.Roles.Add(role);
                existingRoles[def.Code] = role;
            }
        }
        await db.SaveChangesAsync(ct);

        // 3. Seed RolePermission Mappings for Tenant
        var allRolePerms = await db.RolePermissions
            .IgnoreQueryFilters()
            .Where(rp => rp.TenantId == tenantId)
            .Select(rp => new { rp.RoleId, rp.PermissionId })
            .ToListAsync(ct);

        var permSet = allRolePerms.ToHashSet();

        foreach (var def in systemRoleDefs)
        {
            var role = existingRoles[def.Code];
            var allowedCodes = def.GetAllowedPermissionCodes(existingPerms.Keys);

            foreach (var code in allowedCodes)
            {
                if (existingPerms.TryGetValue(code, out var perm))
                {
                    var pair = new { RoleId = role.Id, PermissionId = perm.Id };
                    if (!permSet.Contains(pair))
                    {
                        db.RolePermissions.Add(new RolePermission
                        {
                            TenantId = tenantId,
                            RoleId = role.Id,
                            PermissionId = perm.Id
                        });
                    }
                }
            }
        }
        await db.SaveChangesAsync(ct);

        // 4. Backfill existing Users with missing RoleId based on UserRole enum
        var unassignedUsers = await db.Users
            .IgnoreQueryFilters()
            .Where(u => u.TenantId == tenantId && u.RoleId == null)
            .ToListAsync(ct);

        foreach (var user in unassignedUsers)
        {
            var targetCode = user.Role switch
            {
                UserRole.Admin => RbacSeedData.SystemRoleCodes.Admin,
                UserRole.HrDirector => RbacSeedData.SystemRoleCodes.HrDirector,
                UserRole.Recruiter => RbacSeedData.SystemRoleCodes.Recruiter,
                UserRole.HiringManager => RbacSeedData.SystemRoleCodes.HiringManager,
                UserRole.Approver => RbacSeedData.SystemRoleCodes.Approver,
                _ => RbacSeedData.SystemRoleCodes.Recruiter
            };

            if (existingRoles.TryGetValue(targetCode, out var matchedRole))
            {
                user.RoleId = matchedRole.Id;
            }
        }
        await db.SaveChangesAsync(ct);
    }

    private record SystemRoleDef(string Code, string Name, string Description, bool IsSuperAdmin, Func<IEnumerable<string>, IEnumerable<string>> GetAllowedPermissionCodes);

    private static List<SystemRoleDef> GetSystemRoleDefinitions() => new()
    {
        new(RbacSeedData.SystemRoleCodes.SuperAdmin, "Super Admin", "Super administrator with cross-tenant management", true, all => all),
        new(RbacSeedData.SystemRoleCodes.Admin, "Administrator", "Full tenant administrator", false, all => all),
        new(RbacSeedData.SystemRoleCodes.HrDirector, "HR Director", "Talent acquisition executive", false, all => all.Where(p => !p.StartsWith("permission:roles:") && !p.StartsWith("permission:settings:"))),
        new(RbacSeedData.SystemRoleCodes.Recruiter, "Recruiter", "Full pipeline recruiter", false, all => all.Where(p => p.StartsWith("permission:applications:") || p.StartsWith("permission:postings:") || p.StartsWith("permission:interviews:") || p.StartsWith("permission:scorecards:") || p.Equals("permission:requisitions:requisitions:create") || p.Equals("permission:requisitions:requisitions:read"))),
        new(RbacSeedData.SystemRoleCodes.HiringManager, "Hiring Manager", "Department manager", false, all => all.Where(p => p.Equals("permission:requisitions:requisitions:create") || p.Equals("permission:requisitions:requisitions:read") || p.Equals("permission:applications:applications:read") || p.StartsWith("permission:interviews:") || p.StartsWith("permission:scorecards:"))),
        new(RbacSeedData.SystemRoleCodes.Approver, "Approver", "Requisition approver", false, all => new[] { "permission:requisitions:requisitions:read", "permission:requisitions:requisitions:approve", "permission:applications:applications:read" }),
        new(RbacSeedData.SystemRoleCodes.Interviewer, "Interviewer", "Interview panelist", false, all => new[] { "permission:interviews:interviews:read", "permission:scorecards:scorecards:submit", "permission:scorecards:scorecards:blind_evaluation" })
    };
}
```

---

### 4.3 Integration into `DbInitializer.cs`

Update `DbInitializer.cs` to invoke `AppDbContextSeed.SeedRbacAsync` during database initialization:

```csharp
// Inside DbInitializer.SeedAsync(...)
await AppDbContextSeed.SeedRbacAsync(db, company.Id, ct);
```

---

## 5. Migration Strategy & Execution Plan

### 5.1 EF Core Migration Specification

**Migration Command:**
```bash
dotnet ef migrations add Module2DynamicRbac --project backend/src/Infrastructure --startup-project backend/src/Api
```

**Generated Migration Skeleton (`backend/src/Infrastructure/Migrations/20260729XXXXXX_Module2DynamicRbac.cs`):**

```csharp
using Microsoft.EntityFrameworkCore.Migrations;

namespace RecruitOps.Infrastructure.Migrations;

public partial class Module2DynamicRbac : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // 1. Create Permissions Table
        migrationBuilder.CreateTable(
            name: "Permissions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Module = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                Feature = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                Action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Permissions", x => x.Id);
            });

        // 2. Create Roles Table
        migrationBuilder.CreateTable(
            name: "Roles",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                IsSystemRole = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                IsSuperAdmin = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Roles", x => x.Id);
            });

        // 3. Create RolePermissions Table
        migrationBuilder.CreateTable(
            name: "RolePermissions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                PermissionId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RolePermissions", x => x.Id);
                table.ForeignKey(
                    name: "FK_RolePermissions_Permissions_PermissionId",
                    column: x => x.PermissionId,
                    principalTable: "Permissions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_RolePermissions_Roles_RoleId",
                    column: x => x.RoleId,
                    principalTable: "Roles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        // 4. Add RoleId to Users Table
        migrationBuilder.AddColumn<Guid>(
            name: "RoleId",
            table: "Users",
            type: "uuid",
            nullable: true);

        // 5. Indexes
        migrationBuilder.CreateIndex(
            name: "IX_Permissions_Code",
            table: "Permissions",
            column: "Code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Permissions_Module_Feature",
            table: "Permissions",
            columns: new[] { "Module", "Feature" });

        migrationBuilder.CreateIndex(
            name: "IX_Roles_TenantId_Code",
            table: "Roles",
            columns: new[] { "TenantId", "Code" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Roles_TenantId_Name",
            table: "Roles",
            columns: new[] { "TenantId", "Name" });

        migrationBuilder.CreateIndex(
            name: "IX_RolePermissions_RoleId_PermissionId",
            table: "RolePermissions",
            columns: new[] { "RoleId", "PermissionId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_RolePermissions_TenantId_RoleId",
            table: "RolePermissions",
            columns: new[] { "TenantId", "RoleId" });

        migrationBuilder.CreateIndex(
            name: "IX_Users_RoleId",
            table: "Users",
            column: "RoleId");

        migrationBuilder.AddForeignKey(
            name: "FK_Users_Roles_RoleId",
            table: "Users",
            column: "RoleId",
            principalTable: "Roles",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(name: "FK_Users_Roles_RoleId", table: "Users");
        migrationBuilder.DropTable(name: "RolePermissions");
        migrationBuilder.DropTable(name: "Roles");
        migrationBuilder.DropTable(name: "Permissions");
        migrationBuilder.DropColumn(name: "RoleId", table: "Users");
    }
}
```

---

## 6. Directory Layout & File Operations Summary

### Files to Create in `Domain`:
- `backend/src/Domain/Entities/Role.cs`
- `backend/src/Domain/Entities/Permission.cs`
- `backend/src/Domain/Entities/RolePermission.cs`

### Files to Update in `Domain`:
- `backend/src/Domain/Entities/User.cs` (Add `RoleId` FK & `RoleEntity` navigation)

### Files to Create in `Infrastructure`:
- `backend/src/Infrastructure/Persistence/Configurations/RoleConfiguration.cs`
- `backend/src/Infrastructure/Persistence/Configurations/PermissionConfiguration.cs`
- `backend/src/Infrastructure/Persistence/Configurations/RolePermissionConfiguration.cs`
- `backend/src/Infrastructure/Persistence/Configurations/UserConfiguration.cs`
- `backend/src/Infrastructure/Persistence/Seed/RbacSeedData.cs`
- `backend/src/Infrastructure/Persistence/Seed/AppDbContextSeed.cs`

### Files to Update in `Infrastructure`:
- `backend/src/Infrastructure/Persistence/AppDbContext.cs`
- `backend/src/Infrastructure/Persistence/DbInitializer.cs`

---

## 7. Conclusion & Handoff Readiness

This architecture guarantees a complete, production-grade implementation for Requirement R2 in Milestone 2. All relations, constraints, indexes, cascade policies, seeding scripts, and EF Core migration steps have been thoroughly verified against the RecruitOps codebase standards.
