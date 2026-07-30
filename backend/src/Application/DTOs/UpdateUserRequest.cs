namespace RecruitOps.Application.DTOs;

public record UpdateUserRequest(
    string DisplayName,
    Guid? RoleId = null,
    string? Role = null);
