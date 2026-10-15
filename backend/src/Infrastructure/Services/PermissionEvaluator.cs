using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using RecruitOps.Application.Interfaces;
using RecruitOps.Infrastructure.Persistence;

namespace RecruitOps.Infrastructure.Services;

public sealed class PermissionEvaluator : IPermissionEvaluator
{
    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PermissionEvaluator> _logger;
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(10);

    /// <summary>Bumped by every role change, and part of every cache key — so one increment makes
    /// every cached permission set unreachable at once. Role edits are rare, and a precise
    /// role→users index would have to track every assignment as well.
    /// <para>Static because the cache is a singleton and this class is scoped. Like the login
    /// throttle, it is per process: fine for one instance per company (ADR-0004), and the
    /// absolute expiry below bounds staleness if a second replica is ever added.</para></summary>
    private static long _generation;

    public PermissionEvaluator(
        AppDbContext db,
        IMemoryCache cache,
        ILogger<PermissionEvaluator> logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    public async Task<bool> HasPermissionAsync(
        Guid userId, Guid tenantId, string permissionCode, CancellationToken ct = default)
    {
        var permissions = await GetUserPermissionsAsync(userId, tenantId, ct);
        return permissions.Contains(permissionCode);
    }

    public async Task<IReadOnlySet<string>> GetUserPermissionsAsync(
        Guid userId, Guid tenantId, CancellationToken ct = default)
    {
        var cacheKey = GetCacheKey(userId, tenantId);

        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            // Absolute, not sliding. A sliding expiry never lapses for someone who keeps working,
            // so before invalidation was real a revoked permission lived until the process
            // restarted. Absolute bounds any change that bypasses invalidation (a direct
            // database edit, another replica) to one window.
            entry.SetAbsoluteExpiration(CacheExpiration);

            var user = await _db.Users
                .AsNoTracking()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId && u.IsActive, ct);

            if (user == null)
            {
                return (IReadOnlySet<string>)new HashSet<string>();
            }

            if (user.IsSuperAdmin || user.Role == Domain.Enums.UserRole.SuperAdmin)
            {
                var allPermissions = await _db.Permissions
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .Select(p => p.Code)
                    .ToListAsync(ct);

                if (allPermissions.Count == 0)
                {
                    allPermissions = RbacSeedData.GetCanonicalPermissions().Select(p => p.Code).ToList();
                }

                return (IReadOnlySet<string>)allPermissions.ToHashSet(StringComparer.OrdinalIgnoreCase);
            }

            Guid? targetRoleId = user.RoleId;
            string? systemRoleCode = user.Role.ToString();

            if (!targetRoleId.HasValue && !string.IsNullOrWhiteSpace(systemRoleCode))
            {
                var systemRole = await _db.Roles
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(r => r.IsSystemRole && r.Code == systemRoleCode, ct);

                targetRoleId = systemRole?.Id;
            }

            HashSet<string> permissionCodes = new(StringComparer.OrdinalIgnoreCase);

            if (targetRoleId.HasValue)
            {
                var dbCodes = await _db.RolePermissions
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    // A deactivated role grants nothing; its holder falls back to their
                    // system role below, exactly as if they held no custom role at all.
                    .Where(rp => rp.RoleId == targetRoleId.Value && rp.Role.IsActive)
                    .Select(rp => rp.Permission.Code)
                    .ToListAsync(ct);

                foreach (var c in dbCodes)
                {
                    permissionCodes.Add(c);
                }
            }

            // Fallback for unseeded test DBs or when user.Role enum is mapped without RolePermission rows:
            if (permissionCodes.Count == 0 && !string.IsNullOrWhiteSpace(systemRoleCode))
            {
                var seedRole = RbacSeedData.GetSystemRoles()
                    .FirstOrDefault(r => r.Code.Equals(systemRoleCode, StringComparison.OrdinalIgnoreCase));
                if (seedRole != null)
                {
                    foreach (var code in seedRole.PermissionCodes)
                    {
                        permissionCodes.Add(code);
                    }
                }
            }

            return (IReadOnlySet<string>)permissionCodes;
        }) ?? (IReadOnlySet<string>)new HashSet<string>();
    }

    public void InvalidateUserPermissionsCache(Guid userId, Guid tenantId)
    {
        _cache.Remove(GetCacheKey(userId, tenantId));
    }

    /// <summary>Until 2026-10-15 this only logged, so a role edit never reached anyone whose
    /// permissions were already cached — see <c>PermissionRevocationTests</c>.</summary>
    public void InvalidateRolePermissionsCache(Guid roleId)
    {
        Interlocked.Increment(ref _generation);
        _logger.LogInformation("Role permissions invalidated for Role {RoleId}", roleId);
    }

    private static string GetCacheKey(Guid userId, Guid tenantId) =>
        $"user_perms_{Interlocked.Read(ref _generation)}_{tenantId}_{userId}";
}
