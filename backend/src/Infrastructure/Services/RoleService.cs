using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using RecruitOps.Application.Common;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;
using RecruitOps.Domain;
using RecruitOps.Domain.Entities;
using RecruitOps.Domain.Enums;
using RecruitOps.Infrastructure.Persistence;

namespace RecruitOps.Infrastructure.Services;

public class RoleService : IRoleService
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenant _tenant;
    private readonly IPermissionEvaluator _permissionEvaluator;
    private readonly IGrantorCeiling _ceiling;

    public RoleService(AppDbContext db, ICurrentTenant tenant, IPermissionEvaluator permissionEvaluator, IGrantorCeiling ceiling)
    {
        _db = db;
        _tenant = tenant;
        _permissionEvaluator = permissionEvaluator;
        _ceiling = ceiling;
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
            r.RolePermissions.Count,
            r.BaseRole?.ToString()
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
            role.UpdatedAt,
            role.BaseRole?.ToString()
        );
    }

    /// <summary>Role codes are identifiers, never numbers. A code like <c>"0"</c> used to reach
    /// <c>Enum.TryParse&lt;UserRole&gt;</c> on assignment and come out as SuperAdmin (ADR-0021).
    /// Assignment no longer parses custom codes at all; this keeps a code from ever looking like
    /// an enum value in the first place.</summary>
    private static readonly Regex RoleCodePattern =
        // \z, not $: in .NET, $ also matches before a trailing newline.
        new(@"^[A-Z][A-Z0-9_]{0,63}\z", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Derives a code from a display name. Names may be Burmese, so anything outside
    /// A-Z/0-9 collapses to underscores, and a name with nothing usable left gets a random code
    /// rather than an empty or colliding one — the name, not the code, is what people read.</summary>
    private static string CodeFromName(string name)
    {
        var code = Regex.Replace(name.ToUpperInvariant(), "[^A-Z0-9]+", "_").Trim('_');
        if (code.Length == 0) return $"ROLE_{Guid.NewGuid():N}"[..13].ToUpperInvariant();
        if (!char.IsAsciiLetterUpper(code[0])) code = "ROLE_" + code;
        return code.Length > 64 ? code[..64].TrimEnd('_') : code;
    }

    /// <summary>Base roles by exact name — never <c>Enum.TryParse</c>, which also accepts numbers
    /// (<c>"0"</c> is SuperAdmin) and comma-joined names (ADR-0021).</summary>
    private static readonly IReadOnlyDictionary<string, UserRole> BaseRolesByName =
        RoleScope.AssignableBaseRoles.ToDictionary(r => r.ToString(), StringComparer.OrdinalIgnoreCase);

    private static UserRole ParseBaseRole(string? name) =>
        !string.IsNullOrWhiteSpace(name) && BaseRolesByName.TryGetValue(name.Trim(), out var baseRole)
            ? baseRole
            : throw new ArgumentException(
                $"Choose a base role for this custom role — one of: {string.Join(", ", BaseRolesByName.Keys)}. " +
                "Admin access is given by assigning the Admin system role.");

    public async Task<RoleDetailDto> CreateRoleAsync(CreateRoleRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("Role name is required.");

        var trimmedName = request.Name.Trim();
        var nameExists = await _db.Roles.AnyAsync(r => r.Name.ToLower() == trimmedName.ToLower(), ct);
        if (nameExists)
            throw new InvalidOperationException($"A role named '{trimmedName}' already exists.");

        var code = string.IsNullOrWhiteSpace(request.Code)
            ? CodeFromName(trimmedName)
            : request.Code.Trim().ToUpperInvariant();

        if (!RoleCodePattern.IsMatch(code))
            throw new InvalidOperationException(
                $"Role code '{code}' is invalid: it must start with a letter and contain only A-Z, 0-9 and underscores.");

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

        var baseRole = ParseBaseRole(request.BaseRole);
        await _ceiling.EnsureCanGrantAsync(baseRole, requestedCodes, ct);

        var role = new Role
        {
            Id = Guid.NewGuid(),
            TenantId = _tenant.TenantId,
            Name = trimmedName,
            Code = code,
            Description = request.Description?.Trim() ?? string.Empty,
            IsSystemRole = false,
            IsSuperAdmin = false,
            IsActive = true,
            BaseRole = baseRole,
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
                .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (role is null) return null;

        if (role.IsSystemRole)
            throw new InvalidOperationException("System roles are pre-configured and immutable.");

        // Editing — even only removing, or deactivating — changes what the role's holders reach.
        await _ceiling.EnsureCanManageAsync(role.BaseRole ?? UserRole.Interviewer, ct);

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

        var baseRole = request.BaseRole is null ? role.BaseRole : ParseBaseRole(request.BaseRole);

        // Only what is being granted: resubmitting a permission the actor lacks — a rename does —
        // is not a grant. Reactivating is: every permission the role carries comes back at once.
        // So is widening the base role: holders get the new reach *with* every permission they
        // already had, a combination the actor must hold themselves (security review of option
        // A, finding 2). Narrowing — a base the old one covers — takes nothing new.
        var reactivating = request.IsActive && !role.IsActive;
        var widening = baseRole != role.BaseRole
                       && !(role.BaseRole is { } oldBase && baseRole is { } newBase && RoleScope.Covers(oldBase, newBase));

        // The same question per holder, because holders are what actually move: old data can have
        // holders on another enum than the base (a role coded "5" gave Approver), and the next
        // edit — a rename — syncs them all. Lifting one past their reach is widening; moving
        // anyone at all is managing them (re-review of ADR-0021 option A, L1b).
        var newHolderRole = Role.HolderRoleFor(request.IsActive, baseRole);
        var movingFrom = await HoldersToSync(role.Id, newHolderRole).Select(u => u.Role).Distinct().ToListAsync(ct);
        foreach (var current in movingFrom)
        {
            await _ceiling.EnsureCanManageAsync(current, ct);
            if (!RoleScope.Covers(current, newHolderRole)) widening = true;
        }
        var heldBefore = role.RolePermissions.Select(rp => rp.Permission.Code).ToHashSet(StringComparer.Ordinal);
        var granted = reactivating || widening
            ? requestedCodes
            : requestedCodes.Where(c => !heldBefore.Contains(c)).ToList();
        if (granted.Count > 0 || baseRole != role.BaseRole)
            await _ceiling.EnsureCanGrantAsync(baseRole ?? UserRole.Interviewer, granted, ct);

        role.BaseRole = baseRole;
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

        await SyncHoldersAsync(role, ct);

        await _db.SaveChangesAsync(ct);
        _permissionEvaluator.InvalidateRolePermissionsCache(role.Id);

        return await GetRoleByIdAsync(role.Id, ct);
    }

    /// <summary>Holders carry the role's base role while it is active and Interviewer while it is
    /// not (ADR-0021 option A). Their next request then fails the session check — the token's role
    /// claim no longer matches — so the change reaches them at once, not in 8 hours.</summary>
    private async Task SyncHoldersAsync(Role role, CancellationToken ct)
    {
        var holderRole = role.HolderRole();
        foreach (var holder in await HoldersToSync(role.Id, holderRole).ToListAsync(ct))
            holder.Role = holderRole;
    }

    /// <summary>A SuperAdmin's enum is provisioned out of band, never derived from a role. An Admin
    /// on a custom role only exists from before ADR-0021 (a role coded "1"); it is left for the
    /// pre-deploy audit (NEXT-SESSION) rather than demoted here past the last-Admin floor.</summary>
    private IQueryable<User> HoldersToSync(Guid roleId, UserRole holderRole) =>
        _db.Users.Where(u => u.RoleId == roleId && u.Role != holderRole
                             && u.Role != UserRole.SuperAdmin && u.Role != UserRole.Admin && !u.IsSuperAdmin);

    public async Task<bool> DeleteRoleAsync(Guid id, CancellationToken ct = default)
    {
        var role = await _db.Roles
            .Include(r => r.Users)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (role is null) return false;

        if (role.IsSystemRole)
            throw new InvalidOperationException("Pre-configured system roles cannot be deleted.");

        await _ceiling.EnsureCanManageAsync(role.BaseRole ?? UserRole.Interviewer, ct);

        var activeUsersCount = role.Users.Count(u => u.IsActive);
        if (activeUsersCount > 0)
            throw new InvalidOperationException($"Cannot delete role '{role.Name}' because it is assigned to {activeUsersCount} active user(s).");

        _db.Roles.Remove(role);
        await _db.SaveChangesAsync(ct);
        _permissionEvaluator.InvalidateRolePermissionsCache(role.Id);
        return true;
    }
}
