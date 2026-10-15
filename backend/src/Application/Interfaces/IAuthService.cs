using RecruitOps.Application.DTOs;

namespace RecruitOps.Application.Interfaces;

public interface IAuthService
{
    /// <summary>Validates credentials and returns a signed token pair, or null if invalid.</summary>
    Task<LoginResponse?> LoginAsync(LoginRequest request, string? ipAddress = null, CancellationToken ct = default);

    /// <summary>Validates a refresh token, rotates it, and returns a new access + refresh token pair.</summary>
    Task<LoginResponse?> RefreshTokenAsync(RefreshRequest request, string? ipAddress = null, CancellationToken ct = default);

    /// <summary>Revokes a refresh token.</summary>
    Task<bool> RevokeTokenAsync(string refreshToken, string? ipAddress = null, CancellationToken ct = default);
}
