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

    /// <summary>
    /// The login response must carry the user's permission codes.
    ///
    /// This is a regression guard, not a nicety. `LoginResponse` previously had no
    /// `Permissions` member at all, so the SPA's `session.permissions` was permanently
    /// `undefined` — and the client's `hasPermission()` read "no permissions field" as
    /// "permissions unknown, allow", handing every user the full admin UI. Nothing on
    /// either side failed; the contract just quietly had a hole in it.
    /// </summary>
    [Fact]
    public async Task Login_Response_Carries_Permission_Codes()
    {
        var res = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = CustomWebAppFactory.AdminEmail, Password = CustomWebAppFactory.AdminPassword });

        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<LoginResponse>();

        Assert.NotNull(body);
        Assert.NotNull(body!.Permissions);
        Assert.NotEmpty(body.Permissions);
        Assert.All(body.Permissions, code => Assert.StartsWith("permission:", code));
    }

    /// <summary>
    /// The field has to survive JSON serialization under the real casing policy — asserting
    /// on the deserialized DTO alone would still pass if the property never reached the wire.
    /// </summary>
    [Fact]
    public async Task Login_Response_Serializes_Permissions_To_The_Wire()
    {
        var res = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = CustomWebAppFactory.AdminEmail, Password = CustomWebAppFactory.AdminPassword });

        res.EnsureSuccessStatusCode();
        var raw = await res.Content.ReadAsStringAsync();

        Assert.Contains("permissions", raw, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("permission:", raw, StringComparison.Ordinal);
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
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = CustomWebAppFactory.AdminEmail, Password = CustomWebAppFactory.AdminPassword });
        var body = await login.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.AccessToken));

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", body.AccessToken);

        var response = await client.GetAsync("/api/departments");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
