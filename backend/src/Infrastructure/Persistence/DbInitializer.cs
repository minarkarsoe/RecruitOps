using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RecruitOps.Domain.Entities;
using RecruitOps.Domain.Enums;

namespace RecruitOps.Infrastructure.Persistence;

/// <summary>Development-only seed of a single tenant + admin user, system roles, and permissions (Requirement R2).</summary>
public static class DbInitializer
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();

        await SeedPermissionsAndRolesAsync(db, ct);

        var config = sp.GetRequiredService<IConfiguration>();
        var email = config["Seed:AdminEmail"];
        var password = config["Seed:AdminPassword"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return; // nothing to seed without explicit, non-committed credentials

        if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == email, ct))
            return;

        var company = new Company { Name = "Default Company", Slug = "default" };
        db.Companies.Add(company);

        var adminRole = await db.Roles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.IsSystemRole && r.Code == "Admin", ct);

        var hasher = sp.GetRequiredService<IPasswordHasher<User>>();
        var admin = new User
        {
            TenantId = company.Id,
            Email = email,
            DisplayName = "Administrator",
            Role = UserRole.Admin,
            RoleId = adminRole?.Id,
            IsActive = true,
        };
        admin.PasswordHash = hasher.HashPassword(admin, password);
        db.Users.Add(admin);

        await db.SaveChangesAsync(ct);
    }

    public static async Task SeedPermissionsAndRolesAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SeedPermissionsAndRolesAsync(db, ct);
    }

    /// <summary>Idempotently seeds permissions and system roles into database and links existing users without a RoleId to their corresponding seeded Role (Requirement R2).</summary>
    public static async Task SeedPermissionsAndRolesAsync(AppDbContext db, CancellationToken ct = default)
    {
        // 1. Seed canonical permissions idempotently
        var existingPermissions = await db.Permissions
            .IgnoreQueryFilters()
            .ToDictionaryAsync(p => p.Code, ct);

        var canonicalPermissions = RbacSeedData.GetCanonicalPermissions();
        var newPermissions = new List<Permission>();

        foreach (var perm in canonicalPermissions)
        {
            if (!existingPermissions.TryGetValue(perm.Code, out var existing))
            {
                newPermissions.Add(perm);
                existingPermissions[perm.Code] = perm;
            }
        }

        if (newPermissions.Count > 0)
        {
            db.Permissions.AddRange(newPermissions);
            await db.SaveChangesAsync(ct);
        }

        // 2. Seed system roles idempotently
        var existingSystemRoles = await db.Roles
            .IgnoreQueryFilters()
            .Include(r => r.RolePermissions)
            .Where(r => r.IsSystemRole)
            .ToListAsync(ct);

        var rolesByCode = existingSystemRoles.ToDictionary(r => r.Code, StringComparer.OrdinalIgnoreCase);

        var systemRoleDefs = RbacSeedData.GetSystemRoles();

        foreach (var roleDef in systemRoleDefs)
        {
            if (!rolesByCode.TryGetValue(roleDef.Code, out var role))
            {
                role = new Role
                {
                    TenantId = null,
                    Name = roleDef.Name,
                    Code = roleDef.Code,
                    Description = roleDef.Description,
                    IsSystemRole = true,
                    IsSuperAdmin = roleDef.IsSuperAdmin,
                    IsActive = true
                };
                db.Roles.Add(role);
                await db.SaveChangesAsync(ct);
                rolesByCode[role.Code] = role;
            }
            else
            {
                role.Name = roleDef.Name;
                role.Description = roleDef.Description;
                role.IsSuperAdmin = roleDef.IsSuperAdmin;
                role.IsActive = true;
            }

            var currentPermissionIds = role.RolePermissions.Select(rp => rp.PermissionId).ToHashSet();
            foreach (var permCode in roleDef.PermissionCodes)
            {
                if (existingPermissions.TryGetValue(permCode, out var perm))
                {
                    if (!currentPermissionIds.Contains(perm.Id))
                    {
                        role.RolePermissions.Add(new RolePermission
                        {
                            RoleId = role.Id,
                            PermissionId = perm.Id,
                            AssignedAt = DateTimeOffset.UtcNow
                        });
                    }
                }
            }
        }

        await db.SaveChangesAsync(ct);

        // 3. Link existing users without a RoleId to their corresponding seeded Role
        var usersWithoutRoleId = await db.Users
            .IgnoreQueryFilters()
            .Where(u => u.RoleId == null)
            .ToListAsync(ct);

        if (usersWithoutRoleId.Count > 0)
        {
            foreach (var user in usersWithoutRoleId)
            {
                var roleCode = user.Role.ToString();
                if (rolesByCode.TryGetValue(roleCode, out var matchedRole))
                {
                    user.RoleId = matchedRole.Id;
                    if (matchedRole.IsSuperAdmin)
                    {
                        user.IsSuperAdmin = true;
                    }
                }
            }

            await db.SaveChangesAsync(ct);
        }
    }
}
