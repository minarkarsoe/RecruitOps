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

        // Resolve role
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
            await AssignRoleToUserAsync(user, request.RoleId, request.Role, ct);
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

            user.RoleId = role.Id;
            if (Enum.TryParse<UserRole>(role.Code, true, out var parsedEnum))
            {
                user.Role = parsedEnum;
            }
            else
            {
                user.Role = UserRole.Recruiter;
            }
        }
        else if (!string.IsNullOrWhiteSpace(roleName))
        {
            if (!Enum.TryParse<UserRole>(roleName.Trim(), true, out var parsedEnum))
            {
                throw new ArgumentException($"Role '{roleName}' is invalid.");
            }

            user.Role = parsedEnum;

            var sysRole = await _db.Roles
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.IsSystemRole && r.Code.ToLower() == roleName.Trim().ToLower(), ct);

            if (sysRole is not null)
            {
                user.RoleId = sysRole.Id;
            }
        }
    }
}
