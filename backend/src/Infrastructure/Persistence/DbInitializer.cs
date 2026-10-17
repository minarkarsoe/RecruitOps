using System.Net.Mail;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RecruitOps.Application.Interfaces;
using RecruitOps.Domain.Entities;
using RecruitOps.Domain.Enums;

namespace RecruitOps.Infrastructure.Persistence;

/// <summary>What a database needs before the API can serve it (ADR-0004: unattended installs).
/// Reference data (permissions, system roles) is seeded in every environment; the first Admin
/// comes from configuration — see <see cref="SeedAdminAsync"/>.</summary>
public static class DbInitializer
{
    /// <summary>Production minimum for a bootstrap password: it creates the most powerful account
    /// in the company, and it sits in a deployment file until someone removes it.</summary>
    public const int MinimumBootstrapPasswordLength = 12;

    /// <summary>Creates the company and its first Admin from <c>Seed:AdminEmail</c> /
    /// <c>Seed:AdminPassword</c>.
    /// <para><b>Development:</b> whenever that email is not yet a user — a convenience over a
    /// disposable database.</para>
    /// <para><b>Everywhere else:</b> only while the database holds no users at all — the first run
    /// of an install. Until 2026-10-15 this was Development-only, so a production install had no way
    /// to create its first user. Once anyone exists the settings are ignored, with a warning, so a
    /// value left in a deployment file can never mint a second Admin later. A password shorter than
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

        // Reused when present: adding a second "default" company for a new seed email is refused by
        // the unique slug index, and the API then never started. TimeZoneId is set on purpose: an
        // invitation rendered in UTC is the failure the field exists to prevent (ADR-0026).
        var company = await db.Companies.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Slug == "default", ct);
        if (company is null)
        {
            company = new Company
            {
                Name = "Default Company",
                Slug = "default",
                TimeZoneId = "Asia/Yangon",
            };
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

        // The first Admin is the root of every later access change, so its creation is the first
        // line of the audit log (ADR-0027). Same context, same save.
        await sp.GetRequiredService<IAuditTrail>().RecordAsync(AuditAction.UserCreated,
            AuditTarget.User(admin.Id, admin.DisplayName),
            $"Created the first Administrator ({email}) from install configuration.",
            new { email, role = admin.Role.ToString(), source = "Seed:AdminEmail" },
            actor: new AuditActor(null, "System (install)"), tenantId: company.Id, ct: ct);

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Created the first Administrator account.");
    }

    public static async Task SeedPermissionsAndRolesAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SeedPermissionsAndRolesAsync(db, ct);
    }

    private static void RecordSeedChange(AppDbContext db, AuditAction action, Role role, string summary, object details) =>
        db.AuditLogEntries.Add(new AuditLogEntry
        {
            TenantId = null,
            Action = action,
            ActorName = "System (startup)",
            TargetType = "Role",
            TargetId = role.Id,
            TargetName = role.Name,
            Summary = summary,
            DetailsJson = System.Text.Json.JsonSerializer.Serialize(details,
                new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)),
        });

    /// <summary>Idempotently seeds permissions and system roles into database and links existing users without a RoleId to their corresponding seeded Role (Requirement R2).</summary>
    public static async Task SeedPermissionsAndRolesAsync(AppDbContext db, CancellationToken ct = default)
    {
        // 1. Seed canonical permissions idempotently
        var existingPermissions = await db.Permissions
            .IgnoreQueryFilters()
            .ToDictionaryAsync(p => p.Code, ct);

        var canonicalPermissions = RbacSeedData.GetCanonicalPermissions();

        // A system role naming a code the catalogue does not have would be skipped when granting —
        // and, since step 2 also removes what a definition no longer names, an install's existing
        // grant of the code it meant would be revoked at the next start without a word. A typo there
        // must stop startup before anything is written, not quietly narrow a role (security review,
        // 2026-10-17, L3).
        var catalogue = canonicalPermissions.Select(p => p.Code).ToHashSet(StringComparer.Ordinal);
        var uncatalogued = RbacSeedData.GetSystemRoles()
            .SelectMany(r => r.PermissionCodes.Where(code => !catalogue.Contains(code)).Select(code => $"{r.Code}: {code}"))
            .ToList();
        if (uncatalogued.Count > 0)
            throw new InvalidOperationException(
                "A system role grants a permission that is not in the catalogue, so seeding would revoke " +
                $"instead of grant. Fix RbacSeedData before starting: {string.Join(", ", uncatalogued)}.");

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
            var created = false;
            var changed = new List<string>();
            if (!rolesByCode.TryGetValue(roleDef.Code, out var role))
            {
                created = true;
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
                // No save here: the role, its permissions and its audit entry go in the one save
                // below, together (ADR-0027 §2; security review of ADR-0028, L8). Ids are
                // client-generated, so the permission rows can point at it already.
                db.Roles.Add(role);
                rolesByCode[role.Code] = role;
            }
            else
            {
                if (role.Name != roleDef.Name) changed.Add($"renamed from “{role.Name}”");
                if (role.IsSuperAdmin != roleDef.IsSuperAdmin) changed.Add($"SuperAdmin flag {role.IsSuperAdmin} → {roleDef.IsSuperAdmin}");
                if (!role.IsActive) changed.Add("reactivated");
                role.Name = roleDef.Name;
                role.Description = roleDef.Description;
                role.IsSuperAdmin = roleDef.IsSuperAdmin;
                role.IsActive = true;
            }

            var granted = new List<string>();

            var currentPermissionIds = role.RolePermissions.Select(rp => rp.PermissionId).ToHashSet();
            foreach (var permCode in roleDef.PermissionCodes)
            {
                if (existingPermissions.TryGetValue(permCode, out var perm))
                {
                    if (!currentPermissionIds.Contains(perm.Id))
                    {
                        granted.Add(perm.Code);
                        role.RolePermissions.Add(new RolePermission
                        {
                            RoleId = role.Id,
                            PermissionId = perm.Id,
                            AssignedAt = DateTimeOffset.UtcNow
                        });
                    }
                }
            }

            // ...and revoke what the definition no longer grants. System roles are immutable through
            // the API, so the seed *is* their definition; until 2026-10-17 this only ever added, and an
            // install kept every permission a later version took away — Recruiter's users:read, which
            // exposed every user's email, was the one that mattered (ADR-0019 amendment).
            var removed = new List<string>();
            if (!created)
            {
                var defined = roleDef.PermissionCodes
                    .Select(code => existingPermissions.TryGetValue(code, out var p) ? p.Id : (Guid?)null)
                    .OfType<Guid>()
                    .ToHashSet();
                var codeById = existingPermissions.Values.ToDictionary(p => p.Id, p => p.Code);
                foreach (var stale in role.RolePermissions.Where(rp => !defined.Contains(rp.PermissionId)).ToList())
                {
                    removed.Add(codeById.TryGetValue(stale.PermissionId, out var code) ? code : stale.PermissionId.ToString());
                    role.RolePermissions.Remove(stale);
                    db.RolePermissions.Remove(stale);
                }
            }

            // ADR-0027: the seeder changes access too — on an upgrade it grants system roles their
            // new permissions — so it records what it did, as the system (security review of
            // ADR-0027, M3). System roles are install-wide, so the entries carry no tenant.
            if (created)
                RecordSeedChange(db, AuditAction.RoleCreated, role,
                    $"Created system role “{role.Name}” with {granted.Count} permission{(granted.Count == 1 ? "" : "s")}.",
                    new { role.Code, permissions = granted });
            else if (changed.Count > 0 || granted.Count > 0 || removed.Count > 0)
                RecordSeedChange(db, AuditAction.RoleUpdated, role,
                    $"Updated system role “{role.Name}” at startup: {string.Join("; ", changed
                        .Append(granted.Count > 0 ? $"{granted.Count} permission{(granted.Count == 1 ? "" : "s")} added" : null)
                        .Append(removed.Count > 0 ? $"{removed.Count} permission{(removed.Count == 1 ? "" : "s")} removed" : null)
                        .OfType<string>())}.",
                    new { changes = changed, permissionsAdded = granted, permissionsRemoved = removed });
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
