namespace RecruitOps.Application.DTOs;

public record CreateUserRequest(
    string Email,
    string DisplayName,
    string Password,
    Guid? RoleId = null,
    string? Role = null);
