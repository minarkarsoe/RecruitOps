namespace RecruitOps.Application.DTOs;

public record UserQueryParameters(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    Guid? RoleId = null,
    bool? IsActive = null);
