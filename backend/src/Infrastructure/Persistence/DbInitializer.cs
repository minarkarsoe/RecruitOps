using System.Net.Mail;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RecruitOps.Domain.Entities;
using RecruitOps.Domain.Enums;

namespace RecruitOps.Infrastructure.Persistence;

/// <summary>What a database needs before the API can serve it (ADR-0004: unattended installs).
/// <para>Until 2026-10-15 all of this ran in Development only, so a production install had no
/// permissions or system roles in its database and no way to create its first user.</para></summary>
public static class DbInitializer
{
    /// <summary>Production minimum for a bootstrap password: it creates the most powerful account
    /// in the company, and it sits in a deployment file until someone removes it.</summary>
    public const int MinimumBootstrapPasswordLength = 12;

    /// <summary>Canonical permissions and system roles. Reference data, not test data: every
    /// environment, every startup, idempotently.</summary>
    public static async Task SeedReferenceDataAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        await SeedPermissionsAndRolesAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>(), ct);
    }

    /// <summary>Creates the company and its first Admin from <c>Seed:AdminEmail</c> /
    /// <c>Seed:AdminPassword</c>.
    /// <para><b>Development:</b> whenever that email is not yet a user — a convenience over a
    /// disposable database.</para>
    /// <para><b>Everywhere else:</b> only while the database holds no users at all — the first run
    /// of an install. Once anyone exists the settings are ignored, with a warning, so a value left in
    /// a deployment file can never mint a second Admin later. A password shorter than
    /// <see cref="MinimumBootstrapPasswordLength"/> stops startup instead of creating a weak Admin.</para></summary>
    public static async Task SeedAdminAsync(IServiceProvider services, bool firstRunOnly, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DbInitializer).FullName!);

        var config = sp.GetRequiredService<IConfiguration>();
        var email = config["Seed:AdminEmail"]?.Trim().ToLowerInvariant();
        var password = config["Seed:AdminPassword"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return; // nothing to seed without explicit, non-committed credentials

        // An address login will accept and the column can hold — otherwise this creates an
        // account nobody can sign in to, or fails deep in the database layer.
        if (email.Length > 256 || !MailAddress.TryCreate(email, out var parsed) || parsed.Address != email)
            throw new InvalidOperationException(
                "Seed:AdminEmail must be a single email address of at most 256 characters.");

        if (firstRunOnly)
        {
            if (await db.Users.IgnoreQueryFilters().AnyAsync(ct))
            {
                logger.LogWarning(
                    "Seed:AdminEmail is set but this install already has users, so it was ignored. " +
                    "Remove Seed:AdminEmail and Seed:AdminPassword from the deployment configuration.");
                return;
            }

            if (password.Length < MinimumBootstrapPasswordLength)
                throw new InvalidOperationException(
                    $"Seed:AdminPassword must be at least {MinimumBootstrapPasswordLength} characters.");
        }
        else if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email.ToLower() == email, ct))
        {
            return;
        }

        // Reused when present. The Development path used to add a second "default" company for a
        // new seed email, which the unique slug index refuses — and the API then never started.
        var company = await db.Companies.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Slug == "default", ct);
        if (company is null)
        {
            company = new Company { Name = "Default Company", Slug = "default" };
            db.Companies.Add(company);
        }

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
        logger.LogInformation("Created the first Administrator account ({Email}).", email);
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
                // Linking only. This used to set IsSuperAdmin for a SuperAdmin-role user, which made
                // a SuperAdmin minted through the old "role": "0" path permanent on the next startup
                // (ADR-0021). The flag is a grant, and seeding reference data grants nothing.
                if (rolesByCode.TryGetValue(roleCode, out var matchedRole))
                {
                    user.RoleId = matchedRole.Id;
                }
            }

            await db.SaveChangesAsync(ct);
        }
    }
}
