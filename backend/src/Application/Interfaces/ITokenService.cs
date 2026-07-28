using RecruitOps.Domain.Entities;

namespace RecruitOps.Application.Interfaces;

public record TokenResult(string AccessToken, DateTimeOffset ExpiresAtUtc);

public interface ITokenService
{
    /// <summary>Mints a signed JWT carrying the user's id, tenant_id and role.</summary>
    TokenResult CreateToken(User user);
}
