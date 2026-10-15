using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using RecruitOps.Api.Auth;
using RecruitOps.Application.DTOs;
using Xunit;

namespace RecruitOps.Api.Tests;

/// <summary>What a fresh install outside Development starts with (ADR-0004).
/// <para>Found by the 2026-10-15 security review: all seeding was Development-only, so a production
/// install had no permissions or system roles in its database (Role Builder empty), and no way to
/// create its first user at all.</para>
/// <para>Each test builds its own factory: these are about what an empty database becomes.</para></summary>
public class InstallBootstrapTests
{
    private const string StrongPassword = "Correct-Horse-Battery-9";

    private static WebApplicationFactory<Program> Production(
        CustomWebAppFactory factory, string? adminEmail = null, string? adminPassword = null) =>
        factory.WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Production");
            if (adminEmail is not null) b.UseSetting("Seed:AdminEmail", adminEmail);
            if (adminPassword is not null) b.UseSetting("Seed:AdminPassword", adminPassword);
        });

    private static async Task<HttpStatusCode> LoginAsync(WebApplicationFactory<Program> app, string email, string password) =>
        (await app.CreateClient().PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = email, Password = password })).StatusCode;

    [Fact]
    public async Task Production_Seeds_The_Permissions_And_System_Roles()
    {
        using var factory = new CustomWebAppFactory();
        var app = Production(factory);

        var client = app.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Tenant", factory.TenantA.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", Roles.Admin);
        client.DefaultRequestHeaders.Add("X-Test-UserId", factory.AdminUserId.ToString());

        var roles = await client.GetFromJsonAsync<List<RoleDetailDto>>("/api/roles");
        Assert.Equal(7, roles!.Count(r => r.IsSystemRole));
    }

    [Fact]
    public async Task The_First_Admin_Is_Bootstrapped_From_Configuration_On_An_Empty_Install()
    {
        using var factory = new CustomWebAppFactory();
        var app = Production(factory, "first.admin@customer.test", StrongPassword);

        Assert.Equal(HttpStatusCode.OK, await LoginAsync(app, "first.admin@customer.test", StrongPassword));
    }

    [Fact]
    public async Task Bootstrap_Settings_Are_Ignored_Once_Anyone_Exists()
    {
        using var factory = new CustomWebAppFactory();
        var first = Production(factory, "first.admin@customer.test", StrongPassword);
        first.CreateClient(); // starts the host: bootstraps, and the fixture's own users follow

        // Same database, settings changed — e.g. left in a compose file and edited months later.
        var later = Production(factory, "second.admin@customer.test", StrongPassword);

        Assert.Equal(HttpStatusCode.Unauthorized, await LoginAsync(later, "second.admin@customer.test", StrongPassword));
        Assert.Equal(HttpStatusCode.OK, await LoginAsync(later, "first.admin@customer.test", StrongPassword));
    }

    [Fact]
    public void A_Weak_Bootstrap_Password_Stops_Startup_Rather_Than_Creating_A_Weak_Admin()
    {
        using var factory = new CustomWebAppFactory();
        var app = Production(factory, "first.admin@customer.test", "short");

        Assert.ThrowsAny<Exception>(() => app.CreateClient());
    }
}
