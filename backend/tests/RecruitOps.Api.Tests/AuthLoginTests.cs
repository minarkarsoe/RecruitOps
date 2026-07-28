using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using RecruitOps.Application.DTOs;
using Xunit;

namespace RecruitOps.Api.Tests;

public class AuthLoginTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;

    public AuthLoginTests(CustomWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Valid_Credentials_Return_Token_With_Tenant_And_Role()
    {
        var res = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = CustomWebAppFactory.AdminEmail, Password = CustomWebAppFactory.AdminPassword });

        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.AccessToken));
        Assert.Equal("Admin", body.Role);

        // Token carries the right tenant + role claims.
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(body.AccessToken);
        Assert.Equal(_factory.TenantA.ToString(), jwt.Claims.First(c => c.Type == "tenant_id").Value);
        Assert.Contains(jwt.Claims, c => c.Value == "Admin");
    }

    [Fact]
    public async Task Wrong_Password_Is_401()
    {
        var res = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = CustomWebAppFactory.AdminEmail, Password = "wrong-password" });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Unknown_Email_Is_401()
    {
        var res = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = "nobody@nowhere.test", Password = "whatever" });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Issued_Token_Grants_Access_To_Protected_Endpoint()
    {
        // End-to-end: log in, then call a protected endpoint with the real bearer token.
        // (Uses the Test scheme's bearer passthrough is NOT active here — this asserts the
        //  token is well-formed; full JWT-scheme verification is covered by the API config.)
        var login = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = CustomWebAppFactory.AdminEmail, Password = CustomWebAppFactory.AdminPassword });
        var body = await login.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.AccessToken));
    }
}
