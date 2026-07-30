using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using RecruitOps.Application.Interfaces;
using RecruitOps.Domain.Entities;

namespace RecruitOps.Infrastructure.Services;

/// <summary>Issues self-issued HS256 JWTs. Claims: sub, tenant_id, role, email, name.
/// The role claim uses ClaimTypes.Role to match the API's token validation.</summary>
public class JwtTokenService : ITokenService
{
    private const int LifetimeHours = 8;
    private readonly IConfiguration _config;
    private readonly TimeProvider _clock;

    public JwtTokenService(IConfiguration config, TimeProvider clock)
    {
        _config = config;
        _clock = clock;
    }

    public TokenResult CreateToken(User user)
    {
        var jwt = _config.GetSection("Jwt");
        var key = jwt["Key"];
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("Jwt:Key is not configured. Set it via user-secrets or env.");

        var expires = _clock.GetUtcNow().AddHours(LifetimeHours);
        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim("tenant_id", user.TenantId.ToString()),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("name", user.DisplayName),
            new Claim("is_super_admin", user.IsSuperAdmin.ToString().ToLowerInvariant()),
        };

        var token = new JwtSecurityToken(
            issuer: jwt["Issuer"],
            audience: jwt["Audience"],
            claims: claims,
            expires: expires.UtcDateTime,
            signingCredentials: creds);

        return new TokenResult(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
