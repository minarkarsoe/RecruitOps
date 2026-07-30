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
            entry.SetSlidingExpiration(CacheExpiration);

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
                    .Where(rp => rp.RoleId == targetRoleId.Value)
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

    public void InvalidateRolePermissionsCache(Guid roleId)
    {
        _logger.LogInformation("Role permissions invalidated for Role {RoleId}", roleId);
    }

    private static string GetCacheKey(Guid userId, Guid tenantId) => $"user_perms_{tenantId}_{userId}";
}
