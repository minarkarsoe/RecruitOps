using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;
using RecruitOps.Domain.Entities;
using RecruitOps.Domain.Enums;
using Xunit;

namespace RecruitOps.Api.Tests;

/// <summary>A token stops working as soon as the account it describes changes.
/// <para>Found by the 2026-10-15 security review: a JWT lives 8 hours and nothing re-checked it, so
/// a deactivated Admin kept <c>AdminOnly</c> — and, through the handler's role-claim fallback, every
/// Admin permission — until it expired: long enough to create themselves a fresh Admin account.
/// A demoted user likewise kept their old role.</para>
/// <para>These run on the <b>real JWT bearer scheme</b>. Every other suite swaps it for the header
/// based test handler, which is why nothing caught this — no test had ever validated a token.</para></summary>
public class SessionRevalidationTests : IClassFixture<CustomWebAppFactory>
{
    private const string Password = "P@ssw0rd-Test-123";

    private readonly CustomWebAppFactory _factory;
    private readonly WebApplicationFactory<Program> _app;

    public SessionRevalidationTests(CustomWebAppFactory factory)
    {
        _factory = factory;
        _app = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            s.PostConfigure<AuthenticationOptions>(o => o.DefaultScheme = JwtBearerDefaults.AuthenticationScheme)));
    }

    private async Task<HttpClient> SignedInAsync(string email, string password = Password)
    {
        var client = _app.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = password });
        res.EnsureSuccessStatusCode();
        var token = (await res.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<(Guid Id, string Email)> CreateUserAsync(HttpClient admin, string role)
    {
        var email = $"session.{Guid.NewGuid():N}@alpha.test";
        var res = await admin.PostAsJsonAsync("/api/users", new CreateUserRequest(email, "Session Subject", Password, null, role));
        res.EnsureSuccessStatusCode();
        return ((await res.Content.ReadFromJsonAsync<UserDetailDto>())!.Id, email);
    }

    private Task<HttpClient> AdminAsync() =>
        SignedInAsync(CustomWebAppFactory.AdminEmail, CustomWebAppFactory.AdminPassword);

    [Fact]
    public async Task A_Valid_Token_Is_Accepted()
    {
        // Proves the scheme swap works, so the refusals below are refusals and not misconfiguration.
        var admin = await AdminAsync();
        var (_, email) = await CreateUserAsync(admin, "Recruiter");

        var recruiter = await SignedInAsync(email);
        Assert.Equal(HttpStatusCode.OK, (await recruiter.GetAsync("/api/requisitions")).StatusCode);
    }

    [Fact]
    public async Task A_Deactivated_Users_Token_Stops_Working_Immediately()
    {
        var admin = await AdminAsync();
        var (id, email) = await CreateUserAsync(admin, "Admin");
        var other = await SignedInAsync(email);
        Assert.Equal(HttpStatusCode.OK, (await other.GetAsync("/api/users")).StatusCode);

        (await admin.PutAsync($"/api/users/{id}/deactivate", null)).EnsureSuccessStatusCode();

        // The backdoor the review described: a deactivated Admin creating a fresh Admin.
        var backdoor = await other.PostAsJsonAsync("/api/users",
            new CreateUserRequest($"backdoor.{Guid.NewGuid():N}@alpha.test", "Backdoor", Password, null, "Admin"));
        Assert.Equal(HttpStatusCode.Unauthorized, backdoor.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await other.GetAsync("/api/requisitions")).StatusCode);
    }

    [Fact]
    public async Task A_Demoted_Users_Old_Token_Stops_Working_And_A_New_One_Carries_The_New_Role()
    {
        var admin = await AdminAsync();
        var (id, email) = await CreateUserAsync(admin, "Admin");
        var before = await SignedInAsync(email);
        Assert.Equal(HttpStatusCode.OK, (await before.GetAsync("/api/users")).StatusCode);

        (await admin.PutAsJsonAsync($"/api/users/{id}", new UpdateUserRequest("Session Subject", null, "Recruiter")))
            .EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Unauthorized, (await before.GetAsync("/api/users")).StatusCode);

        var after = await SignedInAsync(email);
        Assert.Equal(HttpStatusCode.Forbidden, (await after.GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await after.GetAsync("/api/requisitions")).StatusCode);
    }

    [Fact]
    public async Task A_Token_For_An_Account_That_Does_Not_Exist_Is_Refused()
    {
        // Correctly signed, but for nobody — e.g. a row removed, or a token from another install
        // that shares a signing key by mistake.
        using var scope = _app.Services.CreateScope();
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var ghost = tokens.CreateToken(new User
        {
            Id = Guid.NewGuid(), TenantId = _factory.TenantA, Email = "ghost@alpha.test",
            DisplayName = "Ghost", Role = UserRole.Admin, IsActive = true,
        });

        var client = _app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ghost.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/requisitions")).StatusCode);
    }
}
