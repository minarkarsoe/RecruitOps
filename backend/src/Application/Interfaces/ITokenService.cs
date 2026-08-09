using RecruitOps.Domain.Entities;

namespace RecruitOps.Application.Interfaces;

public record TokenResult(
    string AccessToken,
    DateTimeOffset ExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAtUtc);

public interface ITokenService
{
    /// <summary>Mints a signed JWT carrying the user's id, tenant_id and role along with a secure random refresh token.</summary>
    TokenResult CreateTokens(User user);

    /// <summary>Backwards compatible alias for CreateTokens.</summary>
    TokenResult CreateToken(User user) => CreateTokens(user);

    /// <summary>Generates a cryptographically secure random token string.</summary>
    string GenerateSecureRandomToken();
}
