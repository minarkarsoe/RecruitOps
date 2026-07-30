using RecruitOps.Application.DTOs;

namespace RecruitOps.Application.Interfaces;

public interface IUserService
{
    Task<PagedResult<UserListItemDto>> GetUsersAsync(UserQueryParameters query, CancellationToken ct = default);
    Task<UserDetailDto?> GetUserByIdAsync(Guid id, CancellationToken ct = default);
    Task<UserDetailDto> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default);
    Task<UserDetailDto?> UpdateUserAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default);
    Task<UserDetailDto?> SetUserActiveAsync(Guid id, bool isActive, CancellationToken ct = default);
}
