using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RecruitOps.Application.Common;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;
using RecruitOps.Domain.Entities;
using RecruitOps.Domain.Enums;
using RecruitOps.Infrastructure.Persistence;

namespace RecruitOps.Infrastructure.Services;

public class UserService : IUserService
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _currentUser;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IPermissionEvaluator _permissionEvaluator;

    public UserService(
        AppDbContext db,
        ICurrentTenant tenant,
        ICurrentUser currentUser,
        IPasswordHasher<User> passwordHasher,
        IPermissionEvaluator permissionEvaluator)
    {
        _db = db;
        _tenant = tenant;
        _currentUser = currentUser;
        _passwordHasher = passwordHasher;
        _permissionEvaluator = permissionEvaluator;
    }

    public async Task<PagedResult<UserListItemDto>> GetUsersAsync(UserQueryParameters queryParams, CancellationToken ct = default)
    {
        int page = queryParams.Page < 1 ? 1 : queryParams.Page;
        int pageSize = queryParams.PageSize switch
        {
            < 1 => 20,
            > 100 => 100,
            _ => queryParams.PageSize
        };

        var query = _db.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(queryParams.Search))
        {
            var search = queryParams.Search.Trim().ToLower();
            query = query.Where(u => u.Email.ToLower().Contains(search) || u.DisplayName.ToLower().Contains(search));
        }

        if (queryParams.RoleId.HasValue)
        {
            query = query.Where(u => u.RoleId == queryParams.RoleId.Value);
        }

        if (queryParams.IsActive.HasValue)
        {
            query = query.Where(u => u.IsActive == queryParams.IsActive.Value);
        }

        int totalCount = await query.CountAsync(ct);

        // STEP 1 (SQL): Materialize primitive fields first to avoid EF Core 10 LINQ Enum.ToString() translation errors
        var rows = await query
            .OrderBy(u => u.DisplayName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.DisplayName,
                u.Role,
                u.RoleId,
                RoleName = u.CustomRole != null ? u.CustomRole.Name : null,
                u.IsActive,
                u.CreatedAt
            })
            .ToListAsync(ct);

        // STEP 2 (Memory): Perform Enum.ToString() in memory
        var items = rows.Select(r => new UserListItemDto(
            r.Id,
            r.Email,
            r.DisplayName,
            r.Role.ToString(),
            r.RoleId,
            r.RoleName ?? r.Role.ToString(),
            r.IsActive,
            r.CreatedAt
        )).ToList();

        int totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        return new PagedResult<UserListItemDto>(items, page, pageSize, totalCount, totalPages);
    }

    public async Task<UserDetailDto?> GetUserByIdAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _db.Users
            .AsNoTracking()
            .Include(u => u.CustomRole)
                .ThenInclude(r => r!.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

        if (user is null) return null;

        UserRoleInfoDto? roleDetails = null;
        List<string> permissions = new();

        if (user.CustomRole is not null)
        {
            roleDetails = new UserRoleInfoDto(
                user.CustomRole.Id,
                user.CustomRole.Name,
                user.CustomRole.Code,
                user.CustomRole.Description,
                user.CustomRole.IsSystemRole,
                user.CustomRole.IsSuperAdmin
            );

            permissions = user.CustomRole.RolePermissions
                .Where(rp => rp.Permission != null)
                .Select(rp => rp.Permission.Code)
                .Distinct()
                .OrderBy(p => p)
                .ToList();
        }

        if (permissions.Count == 0 || roleDetails is null)
        {
            var systemRoleCode = user.Role.ToString();
            var systemRoleSeed = RbacSeedData.GetSystemRoles()
                .FirstOrDefault(r => r.Code.Equals(systemRoleCode, StringComparison.OrdinalIgnoreCase));

            if (systemRoleSeed is not null)
            {
                permissions = systemRoleSeed.PermissionCodes.OrderBy(p => p).ToList();

                if (roleDetails is null)
                {
                    var sysRoleEntity = await _db.Roles
                        .AsNoTracking()
                        .IgnoreQueryFilters()
                        .FirstOrDefaultAsync(r => r.IsSystemRole && r.Code == systemRoleCode, ct);

                    if (sysRoleEntity is not null)
                    {
                        roleDetails = new UserRoleInfoDto(
                            sysRoleEntity.Id,
                            sysRoleEntity.Name,
                            sysRoleEntity.Code,
                            sysRoleEntity.Description,
                            sysRoleEntity.IsSystemRole,
                            sysRoleEntity.IsSuperAdmin
                        );
                    }
                }
            }
        }

        // What the API actually enforces for this user — the same set login sends. The custom
        // role's own codes alone understated it: the handler also grants the system role's seed
        // permissions (security review A6, 2026-10-15).
        permissions = (await _permissionEvaluator.GetEffectivePermissionsAsync(
                user.Id, user.TenantId, new[] { user.Role.ToString() }, ct))
            .Order(StringComparer.Ordinal)
            .ToList();

        return new UserDetailDto(
            user.Id,
            user.Email,
            user.DisplayName,
            user.Role.ToString(),
            user.RoleId,
            roleDetails,
            permissions,
            user.IsActive,
            user.IsSuperAdmin,
            user.CreatedAt,
            user.UpdatedAt
        );
    }

    public async Task<UserDetailDto> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
            throw new ArgumentException("Email format is invalid.");

        var trimmedEmail = request.Email.Trim().ToLowerInvariant();
        if (trimmedEmail.Length > 256)
            throw new ArgumentException("Email cannot exceed 256 characters.");

        var globalEmailExists = await _db.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Email.ToLower() == trimmedEmail, ct);

        if (globalEmailExists)
            throw new InvalidOperationException($"A user with email '{request.Email}' already exists.");

        if (string.IsNullOrWhiteSpace(request.DisplayName))
            throw new ArgumentException("DisplayName is required.");

        var trimmedDisplayName = request.DisplayName.Trim();
        if (trimmedDisplayName.Length > 200)
            throw new ArgumentException("DisplayName cannot exceed 200 characters.");

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            throw new ArgumentException("Password must be at least 8 characters long.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = _tenant.TenantId,
            Email = trimmedEmail,
            DisplayName = trimmedDisplayName,
            IsActive = true,
            IsSuperAdmin = false
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        // An explicit role, always. A missing one used to leave the entity default — Recruiter,
        // company-wide reach into every candidate — which is a guess, not a decision (ADR-0021).
        if (!request.RoleId.HasValue && string.IsNullOrWhiteSpace(request.Role))
            throw new ArgumentException("A role is required.");

        await AssignRoleToUserAsync(user, request.RoleId, request.Role, ct);

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        return (await GetUserByIdAsync(user.Id, ct))!;
    }

    public async Task<UserDetailDto?> UpdateUserAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == id, ct);

        if (user is null) return null;

        if (string.IsNullOrWhiteSpace(request.DisplayName))
            throw new ArgumentException("DisplayName is required.");

        var trimmedDisplayName = request.DisplayName.Trim();
        if (trimmedDisplayName.Length > 200)
            throw new ArgumentException("DisplayName cannot exceed 200 characters.");

        user.DisplayName = trimmedDisplayName;

        if (request.RoleId.HasValue || !string.IsNullOrWhiteSpace(request.Role))
        {
            var (previousRole, previousRoleId) = (user.Role, user.RoleId);
            var wasSuperAdmin = user.IsSuperAdmin || previousRole == UserRole.SuperAdmin;

            await AssignRoleToUserAsync(user, request.RoleId, request.Role, ct);

            // Only an actual change is guarded: the edit dialog resends the current role with a
            // new display name. Throwing before SaveChanges discards the tracked change.
            if (user.Role != previousRole || user.RoleId != previousRoleId)
                await GuardRoleChangeAsync(user, previousRole, wasSuperAdmin, ct);
        }

        await _db.SaveChangesAsync(ct);
        _permissionEvaluator.InvalidateUserPermissionsCache(user.Id, user.TenantId);

        return await GetUserByIdAsync(user.Id, ct);
    }

    public async Task<UserDetailDto?> SetUserActiveAsync(Guid id, bool isActive, CancellationToken ct = default)
    {
        var user = await _db.Users
            .Include(u => u.CustomRole)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

        if (user is null) return null;

        if (!isActive)
        {
            // Deactivation safeguards
            if (_currentUser.UserId.HasValue && id == _currentUser.UserId.Value)
            {
                throw new InvalidOperationException("You cannot deactivate your own account.");
            }

            if (!user.IsActive)
            {
                throw new InvalidOperationException("User account is already inactive.");
            }

            bool isTargetAdmin = user.Role == UserRole.Admin
                                || user.Role == UserRole.SuperAdmin
                                || user.IsSuperAdmin
                                || (user.CustomRole != null && user.CustomRole.IsSuperAdmin);

            if (isTargetAdmin)
            {
                int activeAdminCount = await _db.Users
                    .AsNoTracking()
                    .CountAsync(u => u.IsActive && (u.Role == UserRole.Admin || u.Role == UserRole.SuperAdmin || u.IsSuperAdmin), ct);

                if (activeAdminCount <= 1)
                {
                    throw new InvalidOperationException("Cannot deactivate the last active Administrator account.");
                }
            }
        }
        else
        {
            if (user.IsActive)
            {
                throw new InvalidOperationException("User account is already active.");
            }
        }

        user.IsActive = isActive;
        await _db.SaveChangesAsync(ct);
        _permissionEvaluator.InvalidateUserPermissionsCache(user.Id, user.TenantId);

        return await GetUserByIdAsync(user.Id, ct);
    }

    /// <summary>Role changes the directory refuses whoever asks (security review, 2026-10-15).</summary>
    private async Task GuardRoleChangeAsync(User user, UserRole previousRole, bool wasSuperAdmin, CancellationToken ct)
    {
        // Promoting yourself is the escalation; demoting yourself is how the last Admin disappears.
        if (_currentUser.UserId == user.Id)
            throw new InvalidOperationException("You cannot change your own role. Ask another administrator.");

        // SuperAdmin is provisioned out of band (ADR-0021), and its flag outlives a role change —
        // a "demoted" SuperAdmin would keep bypassing every permission check.
        if (wasSuperAdmin)
            throw new InvalidOperationException("SuperAdmin accounts are not managed through the user directory.");

        // The same floor deactivation already enforces: with SuperAdmin unassignable, there is no
        // API route back from a company with no administrator.
        if (previousRole == UserRole.Admin && user.Role != UserRole.Admin)
        {
            var otherActiveAdmins = await _db.Users
                .AsNoTracking()
                .CountAsync(u => u.Id != user.Id && u.IsActive
                                 && (u.Role == UserRole.Admin || u.Role == UserRole.SuperAdmin || u.IsSuperAdmin), ct);
            if (otherActiveAdmins == 0)
                throw new InvalidOperationException("Cannot change the role of the last active Administrator.");
        }
    }

    /// <summary>The system roles the tenant directory may hand out, by exact name.
    /// <para><b>SuperAdmin is absent on purpose.</b> It is the cross-tenant bypass role, and a
    /// tenant Admin who could grant it could grant themselves everything — which is exactly what
    /// <c>Enum.TryParse</c> allowed until 2026-10-15, by name or by its numeric value <c>0</c>
    /// (ADR-0021). It is provisioned out of band, never through this API.</para>
    /// <para>A dictionary of names rather than <c>Enum.TryParse</c>: TryParse also accepts any
    /// numeric string, including values no member has.</para></summary>
    private static readonly IReadOnlyDictionary<string, UserRole> AssignableSystemRoles =
        Enum.GetValues<UserRole>()
            .Where(r => r != UserRole.SuperAdmin)
            .ToDictionary(r => r.ToString(), StringComparer.OrdinalIgnoreCase);

    private static UserRole ParseAssignableRole(string name) =>
        AssignableSystemRoles.TryGetValue(name.Trim(), out var role)
            ? role
            : throw new ArgumentException(
                $"Role '{name.Trim()}' cannot be assigned. Use one of: {string.Join(", ", AssignableSystemRoles.Keys)}.");

    private async Task AssignRoleToUserAsync(User user, Guid? roleId, string? roleName, CancellationToken ct)
    {
        if (roleId.HasValue)
        {
            var role = await _db.Roles
                .FirstOrDefaultAsync(r => r.Id == roleId.Value, ct);

            if (role is null || !role.IsActive)
            {
                throw new InvalidOperationException($"Role with ID '{roleId.Value}' was not found or is inactive.");
            }

            // A custom role used to fall through to UserRole.Recruiter, and the role policies,
            // RoleScope and the seed-role permission fallback all key off that enum — so a role
            // with an empty matrix still reached every candidate in the company. Until a custom
            // role carries its own data scope, assigning one is refused rather than guessed.
            if (!role.IsSystemRole)
            {
                throw new InvalidOperationException(
                    $"Custom role '{role.Name}' cannot be assigned to users yet: a custom role does not " +
                    "define which departments and candidates its holders may reach (ADR-0021).");
            }

            user.Role = ParseAssignableRole(role.Code);
            user.RoleId = role.Id;
        }
        else if (!string.IsNullOrWhiteSpace(roleName))
        {
            user.Role = ParseAssignableRole(roleName);

            var code = user.Role.ToString();
            var sysRole = await _db.Roles
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.IsSystemRole && r.Code == code, ct);

            // Cleared when the system role row is missing, rather than left pointing at whatever
            // role the user held before: the evaluator reads RoleId first, so a stale id would
            // keep granting the old role's permissions under the new role's name.
            user.RoleId = sysRole?.Id;
        }
    }
}
