namespace RecruitOps.Application.DTOs;

public record LoginResponse(
    string AccessToken,
    DateTimeOffset ExpiresAtUtc,
    string Role,
    string DisplayName,
    Guid UserId);
