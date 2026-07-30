using Microsoft.EntityFrameworkCore;
using RecruitOps.Application.Common;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;
using RecruitOps.Domain.Entities;
using RecruitOps.Infrastructure.Persistence;

namespace RecruitOps.Infrastructure.Services;

public class RoleService : IRoleService
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenant _tenant;
    private readonly IPermissionEvaluator _permissionEvaluator;

    public RoleService(AppDbContext db, ICurrentTenant tenant, IPermissionEvaluator permissionEvaluator)
    {
        _db = db;
        _tenant = tenant;
        _permissionEvaluator = permissionEvaluator;
    }

    public async Task<IReadOnlyList<PermissionModuleDto>> GetPermissionsGroupedAsync(CancellationToken ct = default)
    {
        var permissions = await _db.Permissions
            .AsNoTracking()
            .IgnoreQueryFilters()
            .OrderBy(p => p.Module)
            .ThenBy(p => p.Feature)
            .ThenBy(p => p.Action)
            .ToListAsync(ct);

        if (permissions.Count == 0)
        {
            permissions = RbacSeedData.GetCanonicalPermissions();
        }

        var grouped = permissions
            .GroupBy(p => p.Module)
            .Select(gModule => new PermissionModuleDto(
                gModule.Key,
                gModule.GroupBy(p => p.Feature)
                    .Select(gFeature => new PermissionFeatureDto(
                        gFeature.Key,
                        gFeature.Select(p => new PermissionDto(
                            p.Id, p.Code, p.Name, p.Description, p.Module, p.Feature, p.Action
                        )).ToList()
                    )).ToList()
            )).ToList();

        return grouped;
    }

    public async Task<IReadOnlyList<RoleListItemDto>> GetRolesAsync(CancellationToken ct = default)
    {
        var roles = await _db.Roles
            .AsNoTracking()
            .Include(r => r.RolePermissions)
            .Include(r => r.Users)
            .OrderBy(r => r.IsSystemRole ? 0 : 1)
            .ThenBy(r => r.Name)
            .ToListAsync(ct);

        return roles.Select(r => new RoleListItemDto(
            r.Id,
            r.Name,
            r.Code,
            r.Description,
            r.IsSystemRole,
            r.IsSuperAdmin,
            r.IsActive,
            r.Users.Count(u => u.IsActive),
            r.RolePermissions.Count
        )).ToList();
    }

    public async Task<RoleDetailDto?> GetRoleByIdAsync(Guid id, CancellationToken ct = default)
    {
        var role = await _db.Roles
            .AsNoTracking()
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .Include(r => r.Users)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (role is null) return null;

        var assignedPermissions = role.RolePermissions
            .Where(rp => rp.Permission != null)
            .Select(rp => new PermissionDto(
                rp.Permission.Id,
                rp.Permission.Code,
                rp.Permission.Name,
                rp.Permission.Description,
                rp.Permission.Module,
                rp.Permission.Feature,
                rp.Permission.Action
            )).ToList();

        var assignedCodes = assignedPermissions.Select(p => p.Code).ToList();

        // Fallback for unseeded test roles
        if (assignedCodes.Count == 0 && role.IsSystemRole)
        {
            var seedRole = RbacSeedData.GetSystemRoles()
                .FirstOrDefault(r => r.Code.Equals(role.Code, StringComparison.OrdinalIgnoreCase));

            if (seedRole != null)
            {
                var canonicalMap = RbacSeedData.GetCanonicalPermissions()
                    .ToDictionary(p => p.Code, StringComparer.OrdinalIgnoreCase);

                foreach (var code in seedRole.PermissionCodes)
                {
                    if (canonicalMap.TryGetValue(code, out var p))
                    {
                        assignedPermissions.Add(new PermissionDto(
                            p.Id, p.Code, p.Name, p.Description, p.Module, p.Feature, p.Action
                        ));
                    }
                }
                assignedCodes = seedRole.PermissionCodes.ToList();
            }
        }

        return new RoleDetailDto(
            role.Id,
            role.Name,
            role.Code,
            role.Description,
            role.IsSystemRole,
            role.IsSuperAdmin,
            role.IsActive,
            assignedPermissions,
            assignedCodes,
            role.Users.Count(u => u.IsActive),
            role.CreatedAt,
            role.UpdatedAt
        );
    }

    public async Task<RoleDetailDto> CreateRoleAsync(CreateRoleRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("Role name is required.");

        var trimmedName = request.Name.Trim();
        var nameExists = await _db.Roles.AnyAsync(r => r.Name.ToLower() == trimmedName.ToLower(), ct);
        if (nameExists)
            throw new InvalidOperationException($"A role named '{trimmedName}' already exists.");

        var code = string.IsNullOrWhiteSpace(request.Code)
            ? trimmedName.ToUpperInvariant().Replace(" ", "_")
            : request.Code.Trim().ToUpperInvariant();

        var codeExists = await _db.Roles.AnyAsync(r => r.Code.ToLower() == code.ToLower(), ct);
        if (codeExists)
            throw new InvalidOperationException($"A role with code '{code}' already exists.");

        var requestedCodes = request.PermissionCodes?.Distinct().ToList() ?? new List<string>();
        var validPermissions = await _db.Permissions
            .IgnoreQueryFilters()
            .Where(p => requestedCodes.Contains(p.Code))
            .ToListAsync(ct);

        if (validPermissions.Count != requestedCodes.Count)
        {
            var foundCodes = validPermissions.Select(p => p.Code).ToHashSet();
            var invalidCodes = requestedCodes.Where(c => !foundCodes.Contains(c)).ToList();
            throw new InvalidOperationException($"Invalid permission code(s): {string.Join(", ", invalidCodes)}");
        }

        var role = new Role
        {
            Id = Guid.NewGuid(),
            TenantId = _tenant.TenantId,
            Name = trimmedName,
            Code = code,
            Description = request.Description?.Trim() ?? string.Empty,
            IsSystemRole = false,
            IsSuperAdmin = false,
            IsActive = true
        };

        foreach (var perm in validPermissions)
        {
            role.RolePermissions.Add(new RolePermission
            {
                RoleId = role.Id,
                PermissionId = perm.Id,
                AssignedAt = DateTimeOffset.UtcNow
            });
        }

        _db.Roles.Add(role);
        await _db.SaveChangesAsync(ct);

        return (await GetRoleByIdAsync(role.Id, ct))!;
    }

    public async Task<RoleDetailDto?> UpdateRoleAsync(Guid id, UpdateRoleRequest request, CancellationToken ct = default)
    {
        var role = await _db.Roles
            .Include(r => r.RolePermissions)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (role is null) return null;

        if (role.IsSystemRole)
            throw new InvalidOperationException("System roles are pre-configured and immutable.");

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("Role name is required.");

        var trimmedName = request.Name.Trim();
        var nameExists = await _db.Roles.AnyAsync(r => r.Id != id && r.Name.ToLower() == trimmedName.ToLower(), ct);
        if (nameExists)
            throw new InvalidOperationException($"A role named '{trimmedName}' already exists.");

        var requestedCodes = request.PermissionCodes?.Distinct().ToList() ?? new List<string>();
        var validPermissions = await _db.Permissions
            .IgnoreQueryFilters()
            .Where(p => requestedCodes.Contains(p.Code))
            .ToListAsync(ct);

        if (validPermissions.Count != requestedCodes.Count)
        {
            var foundCodes = validPermissions.Select(p => p.Code).ToHashSet();
            var invalidCodes = requestedCodes.Where(c => !foundCodes.Contains(c)).ToList();
            throw new InvalidOperationException($"Invalid permission code(s): {string.Join(", ", invalidCodes)}");
        }

        role.Name = trimmedName;
        role.Description = request.Description?.Trim() ?? string.Empty;
        role.IsActive = request.IsActive;

        // Sync role permissions
        var newPermIds = validPermissions.Select(p => p.Id).ToHashSet();
        var toRemove = role.RolePermissions.Where(rp => !newPermIds.Contains(rp.PermissionId)).ToList();
        foreach (var rp in toRemove)
        {
            _db.RolePermissions.Remove(rp);
        }

        var existingPermIds = role.RolePermissions.Select(rp => rp.PermissionId).ToHashSet();
        foreach (var perm in validPermissions.Where(p => !existingPermIds.Contains(p.Id)))
        {
            role.RolePermissions.Add(new RolePermission
            {
                RoleId = role.Id,
                PermissionId = perm.Id,
                AssignedAt = DateTimeOffset.UtcNow
            });
        }

        await _db.SaveChangesAsync(ct);
        _permissionEvaluator.InvalidateRolePermissionsCache(role.Id);

        return await GetRoleByIdAsync(role.Id, ct);
    }

    public async Task<bool> DeleteRoleAsync(Guid id, CancellationToken ct = default)
    {
        var role = await _db.Roles
            .Include(r => r.Users)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (role is null) return false;

        if (role.IsSystemRole)
            throw new InvalidOperationException("Pre-configured system roles cannot be deleted.");

        var activeUsersCount = role.Users.Count(u => u.IsActive);
        if (activeUsersCount > 0)
            throw new InvalidOperationException($"Cannot delete role '{role.Name}' because it is assigned to {activeUsersCount} active user(s).");

        _db.Roles.Remove(role);
        await _db.SaveChangesAsync(ct);
        _permissionEvaluator.InvalidateRolePermissionsCache(role.Id);
        return true;
    }
}
