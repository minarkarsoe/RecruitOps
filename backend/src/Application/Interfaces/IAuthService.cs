using RecruitOps.Application.DTOs;

namespace RecruitOps.Application.Interfaces;

public interface IAuthService
{
    /// <summary>Validates credentials and returns a signed token, or null if invalid.</summary>
    Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken ct = default);
}
