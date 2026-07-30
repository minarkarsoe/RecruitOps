using Microsoft.EntityFrameworkCore;
using RecruitOps.Application.Common;
using RecruitOps.Domain.Entities;
using RecruitOps.Domain.Enums;
using RecruitOps.Infrastructure.Persistence;
using Xunit;

namespace RecruitOps.Domain.Tests;

public class RbacDomainTests
{
    private class TestTenant : ICurrentTenant
    {
        public Guid TenantId { get; set; } = Guid.NewGuid();
    }

    private AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options, new TestTenant());
    }

    [Fact]
    public void Role_Entity_Initializes_With_Expected_Defaults()
    {
        var tenantId = Guid.NewGuid();
        var role = new Role
        {
            TenantId = tenantId,
            Name = "Custom Recruiter",
            Code = "custom_recruiter",
            Description = "Custom recruiter role",
            IsSystemRole = false,
            IsSuperAdmin = false,
            IsActive = true
        };

        Assert.NotEqual(Guid.Empty, role.Id);
        Assert.Equal(tenantId, role.TenantId);
        Assert.Equal("Custom Recruiter", role.Name);
        Assert.Equal("custom_recruiter", role.Code);
        Assert.Equal("Custom recruiter role", role.Description);
        Assert.False(role.IsSystemRole);
        Assert.False(role.IsSuperAdmin);
        Assert.True(role.IsActive);
        Assert.NotNull(role.RolePermissions);
        Assert.Empty(role.RolePermissions);
        Assert.NotNull(role.Users);
        Assert.Empty(role.Users);
    }

    [Fact]
    public void Permission_Entity_Initializes_With_Expected_Format()
    {
        var permission = new Permission
        {
            Module = "requisitions",
            Feature = "requisitions",
            Action = "approve",
            Name = "Approve Requisitions",
            Description = "Approve job requisitions",
            Code = "permission:requisitions:requisitions:approve"
        };

        Assert.NotEqual(Guid.Empty, permission.Id);
        Assert.Equal("requisitions", permission.Module);
        Assert.Equal("requisitions", permission.Feature);
        Assert.Equal("approve", permission.Action);
        Assert.Equal("Approve Requisitions", permission.Name);
        Assert.Equal("Approve job requisitions", permission.Description);
        Assert.Equal("permission:requisitions:requisitions:approve", permission.Code);
        Assert.NotNull(permission.RolePermissions);
        Assert.Empty(permission.RolePermissions);
    }

    [Fact]
    public void RolePermission_Join_Entity_Links_Role_And_Permission()
    {
        var role = new Role { Name = "Admin", Code = "Admin", IsSystemRole = true };
        var permission = new Permission { Module = "users", Feature = "users", Action = "read", Code = "permission:users:users:read" };
        var assignedAt = DateTimeOffset.UtcNow;

        var rolePermission = new RolePermission
        {
            RoleId = role.Id,
            Role = role,
            PermissionId = permission.Id,
            Permission = permission,
            AssignedAt = assignedAt
        };

        Assert.Equal(role.Id, rolePermission.RoleId);
        Assert.Equal(role, rolePermission.Role);
        Assert.Equal(permission.Id, rolePermission.PermissionId);
        Assert.Equal(permission, rolePermission.Permission);
        Assert.Equal(assignedAt, rolePermission.AssignedAt);
    }

    [Fact]
    public void User_Entity_Supports_CustomRole_And_IsSuperAdmin_With_UserRole_Backwards_Compatibility()
    {
        var role = new Role { Name = "SuperAdmin", Code = "SuperAdmin", IsSuperAdmin = true };
        var user = new User
        {
            Email = "superadmin@recruitops.io",
            DisplayName = "Super Admin",
            Role = UserRole.SuperAdmin,
            RoleId = role.Id,
            CustomRole = role,
            IsSuperAdmin = true
        };

        Assert.Equal(UserRole.SuperAdmin, user.Role);
        Assert.Equal(role.Id, user.RoleId);
        Assert.Equal(role, user.CustomRole);
        Assert.True(user.IsSuperAdmin);
    }

    [Fact]
    public void UserRole_Enum_Contains_All_Required_Roles()
    {
        var enumValues = Enum.GetNames<UserRole>();

        Assert.Contains("SuperAdmin", enumValues);
        Assert.Contains("Admin", enumValues);
        Assert.Contains("HrDirector", enumValues);
        Assert.Contains("Recruiter", enumValues);
        Assert.Contains("HiringManager", enumValues);
        Assert.Contains("Approver", enumValues);
        Assert.Contains("Interviewer", enumValues);
    }

    [Fact]
    public void RbacSeedData_Defines_Canonical_Permissions_Across_9_Modules()
    {
        var permissions = RbacSeedData.GetCanonicalPermissions();

        Assert.NotEmpty(permissions);

        var modules = permissions.Select(p => p.Module).Distinct().ToList();
        var expectedModules = new[]
        {
            "requisitions", "postings", "applications", "interviews",
            "scorecards", "users", "roles", "settings", "system"
        };

        foreach (var expectedModule in expectedModules)
        {
            Assert.Contains(expectedModule, modules);
        }

        foreach (var p in permissions)
        {
            Assert.StartsWith("permission:", p.Code);
            Assert.Equal($"permission:{p.Module}:{p.Feature}:{p.Action}", p.Code);
        }
    }

    [Fact]
    public void RbacSeedData_Defines_7_Default_System_Roles()
    {
        var roles = RbacSeedData.GetSystemRoles();

        Assert.Equal(7, roles.Count);

        var roleCodes = roles.Select(r => r.Code).ToList();
        var expectedRoleCodes = new[]
        {
            "SuperAdmin", "Admin", "HrDirector", "Recruiter",
            "HiringManager", "Approver", "Interviewer"
        };

        foreach (var code in expectedRoleCodes)
        {
            Assert.Contains(code, roleCodes);
        }

        var superAdmin = roles.First(r => r.Code == "SuperAdmin");
        Assert.True(superAdmin.IsSuperAdmin);
        Assert.NotEmpty(superAdmin.PermissionCodes);

        var admin = roles.First(r => r.Code == "Admin");
        Assert.False(admin.IsSuperAdmin);
        Assert.DoesNotContain("permission:system:system:manage", admin.PermissionCodes);
    }

    [Fact]
    public async Task DbInitializer_SeedPermissionsAndRolesAsync_Seeds_Db_And_Links_Existing_Users_Idempotently()
    {
        using var db = CreateDbContext();

        var tenantId = Guid.NewGuid();
        var existingUser1 = new User
        {
            TenantId = tenantId,
            Email = "recruiter@example.com",
            DisplayName = "Test Recruiter",
            Role = UserRole.Recruiter,
            RoleId = null
        };
        var existingUser2 = new User
        {
            TenantId = tenantId,
            Email = "superadmin@example.com",
            DisplayName = "Test SuperAdmin",
            Role = UserRole.SuperAdmin,
            RoleId = null
        };

        db.Users.AddRange(existingUser1, existingUser2);
        await db.SaveChangesAsync();

        // Act 1: Initial seed
        await DbInitializer.SeedPermissionsAndRolesAsync(db);

        var permissionsCount = await db.Permissions.IgnoreQueryFilters().CountAsync();
        var rolesCount = await db.Roles.IgnoreQueryFilters().CountAsync();
        Assert.True(permissionsCount >= 29);
        Assert.Equal(7, rolesCount);

        var recruiterUser = await db.Users.IgnoreQueryFilters().FirstAsync(u => u.Email == "recruiter@example.com");
        Assert.NotNull(recruiterUser.RoleId);
        var recruiterRole = await db.Roles.IgnoreQueryFilters().FirstAsync(r => r.Id == recruiterUser.RoleId);
        Assert.Equal("Recruiter", recruiterRole.Code);

        var superAdminUser = await db.Users.IgnoreQueryFilters().FirstAsync(u => u.Email == "superadmin@example.com");
        Assert.NotNull(superAdminUser.RoleId);
        Assert.True(superAdminUser.IsSuperAdmin);

        // Act 2: Idempotency check (run again second time)
        await DbInitializer.SeedPermissionsAndRolesAsync(db);

        var rolePermissionsCountRun2 = await db.RolePermissions.IgnoreQueryFilters().CountAsync();
        Assert.Equal(permissionsCount, await db.Permissions.IgnoreQueryFilters().CountAsync());
        Assert.Equal(rolesCount, await db.Roles.IgnoreQueryFilters().CountAsync());

        // Act 3: Idempotency check (run third time to guarantee no duplicate join records)
        await DbInitializer.SeedPermissionsAndRolesAsync(db);

        Assert.Equal(permissionsCount, await db.Permissions.IgnoreQueryFilters().CountAsync());
        Assert.Equal(rolesCount, await db.Roles.IgnoreQueryFilters().CountAsync());
        Assert.Equal(rolePermissionsCountRun2, await db.RolePermissions.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task DbInitializer_SeedPermissionsAndRolesAsync_Verifies_Exact_34_Permissions_And_7_System_Roles()
    {
        using var db = CreateDbContext();

        await DbInitializer.SeedPermissionsAndRolesAsync(db);

        var permissions = await db.Permissions.IgnoreQueryFilters().ToListAsync();
        var roles = await db.Roles.IgnoreQueryFilters().Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission).ToListAsync();

        // Verification of requirement R2 & milestone scope:
        // Canonical permission count: 34 permissions total across 9 modules (exceeds the 29 requirement threshold)
        Assert.Equal(34, permissions.Count);
        Assert.True(permissions.Count >= 29);

        // Canonical system roles: 7 default roles
        Assert.Equal(7, roles.Count);

        var roleMap = roles.ToDictionary(r => r.Code);

        // SuperAdmin: Has all 34 permissions
        Assert.True(roleMap["SuperAdmin"].IsSuperAdmin);
        Assert.Equal(34, roleMap["SuperAdmin"].RolePermissions.Count);

        // Admin: Has 33 permissions (all except permission:system:system:manage)
        Assert.False(roleMap["Admin"].IsSuperAdmin);
        Assert.Equal(33, roleMap["Admin"].RolePermissions.Count);
        Assert.DoesNotContain(roleMap["Admin"].RolePermissions, rp => rp.Permission.Code == "permission:system:system:manage");

        // HrDirector: 26 permissions
        Assert.Equal(26, roleMap["HrDirector"].RolePermissions.Count);

        // Recruiter: 18 permissions
        Assert.Equal(18, roleMap["Recruiter"].RolePermissions.Count);

        // HiringManager: 11 permissions
        Assert.Equal(11, roleMap["HiringManager"].RolePermissions.Count);

        // Approver: 2 permissions
        Assert.Equal(2, roleMap["Approver"].RolePermissions.Count);

        // Interviewer: 3 permissions
        Assert.Equal(3, roleMap["Interviewer"].RolePermissions.Count);
    }
}

