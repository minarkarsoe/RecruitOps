using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using RecruitOps.Domain.Entities;
using RecruitOps.Domain.Enums;
using RecruitOps.Infrastructure.Services;
using Xunit;

namespace RecruitOps.Api.Tests;

public class JwtTokenServiceTests
{
    private static JwtTokenService Build()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "recruitops",
            ["Jwt:Audience"] = "recruitops-api",
            ["Jwt:Key"] = "unit-test-signing-key-that-is-long-enough-0123456789",
        }).Build();
        return new JwtTokenService(config, TimeProvider.System);
    }

    [Fact]
    public void CreateToken_Embeds_Tenant_Role_And_Subject()
    {
        var user = new User
        {
            TenantId = Guid.NewGuid(),
            Email = "a@b.test",
            DisplayName = "Test",
            Role = UserRole.Recruiter,
        };

        var result = Build().CreateToken(user);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.AccessToken);

        Assert.Equal(user.TenantId.ToString(), jwt.Claims.First(c => c.Type == "tenant_id").Value);
        Assert.Equal("Recruiter", jwt.Claims.First(c => c.Type == ClaimTypes.Role).Value);
        Assert.Equal(user.Id.ToString(), jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.True(result.ExpiresAtUtc > DateTimeOffset.UtcNow);
    }

    [Fact]
    public void CreateToken_Throws_When_Key_Missing()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "recruitops",
        }).Build();
        var svc = new JwtTokenService(config, TimeProvider.System);

        Assert.Throws<InvalidOperationException>(() => svc.CreateToken(new User()));
    }
}
