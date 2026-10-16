using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
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
    public async Task A_Mixed_Case_Bootstrap_Email_Signs_In_As_Typed()
    {
        using var factory = new CustomWebAppFactory();
        var app = Production(factory, "IT.Lead@Customer.Test", StrongPassword);

        Assert.Equal(HttpStatusCode.OK, await LoginAsync(app, "IT.Lead@Customer.Test", StrongPassword));
    }

    [Theory]
    [InlineData("admin")]           // not an address: login's [EmailAddress] would never accept it
    [InlineData("admin@")]
    [InlineData("@customer.test")]
    public void A_Malformed_Bootstrap_Email_Stops_Startup(string email)
    {
        using var factory = new CustomWebAppFactory();
        Assert.ThrowsAny<Exception>(() => Production(factory, email, StrongPassword).CreateClient());
    }

    [Fact]
    public void A_Bootstrap_Email_Longer_Than_The_Column_Stops_Startup_With_A_Clear_Message()
    {
        using var factory = new CustomWebAppFactory();
        var tooLong = new string('a', 250) + "@customer.test";
        var ex = Assert.ThrowsAny<Exception>(() => Production(factory, tooLong, StrongPassword).CreateClient());
        Assert.Contains("Seed:AdminEmail", ex.ToString());
    }

    /// <summary>Second security review (b)-1: <c>.env.example</c> ships a signing key, and nothing
    /// stopped an install using it — anyone who has read the repository could then mint tokens.</summary>
    [Theory]
    [InlineData("dev-only-signing-key-change-me-at-least-32-chars")] // the published example
    [InlineData("CHANGE_ME_use_a_long_random_secret_at_least_32_bytes")] // backend/src/Api/.env.example
    [InlineData("SuperSecretProductionJwtSigningKey2026_AtLeast32CharsLong!")] // the VPS guide, until 2026-10-16
    [InlineData("too-short-key")]
    public void Production_Refuses_A_Known_Or_Short_Signing_Key(string key)
    {
        using var factory = new CustomWebAppFactory();
        var app = factory.WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Production");
            // An app-configuration source, added after the factory's own in-memory Jwt:Key — a
            // host setting (UseSetting) would be outranked by it and test nothing.
            b.ConfigureAppConfiguration((_, cfg) =>
                cfg.AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Key"] = key }));
        });
        Assert.ThrowsAny<Exception>(() => app.CreateClient());
    }

    [Fact]
    public void A_Weak_Bootstrap_Password_Stops_Startup_Rather_Than_Creating_A_Weak_Admin()
    {
        using var factory = new CustomWebAppFactory();
        var app = Production(factory, "first.admin@customer.test", "short");

        Assert.ThrowsAny<Exception>(() => app.CreateClient());
    }
}
