using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using RecruitOps.Api.Auth;
using Xunit;

namespace RecruitOps.Api.Tests;

/// <summary>Response headers, and which browser origins may call the API.
/// <para>Until 2026-10-15 the localhost dev origins were allowed by CORS in every environment, and
/// API responses could be cached.</para></summary>
public class SecurityHeadersTests : IClassFixture<CustomWebAppFactory>
{
    private const string DevSpaOrigin = "http://localhost:5173";

    private readonly CustomWebAppFactory _factory;

    public SecurityHeadersTests(CustomWebAppFactory factory) => _factory = factory;

    private HttpClient Signed(WebApplicationFactory<Program> app)
    {
        var client = app.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Tenant", _factory.TenantA.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", Roles.Recruiter);
        return client;
    }

    private WebApplicationFactory<Program> Production() =>
        _factory.WithWebHostBuilder(b => b.UseEnvironment("Production"));

    private static Task<HttpResponseMessage> Preflight(HttpClient client, string origin)
    {
        var req = new HttpRequestMessage(HttpMethod.Options, "/api/requisitions");
        req.Headers.Add("Origin", origin);
        req.Headers.Add("Access-Control-Request-Method", "POST");
        req.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");
        return client.SendAsync(req);
    }

    [Theory]
    [InlineData("/api/requisitions")] // a success
    [InlineData("/api/roles")]        // a refusal — 403 for a Recruiter
    public async Task Api_Responses_Carry_The_Headers_And_Are_Not_Cached(string path)
    {
        var res = await Signed(_factory).GetAsync(path);

        // SecurityHeadersMiddleware's values; no-store is the 2026-10-15 addition.
        Assert.Equal("nosniff", res.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", res.Headers.GetValues("X-Frame-Options").Single());
        Assert.Contains("frame-ancestors 'none'", res.Headers.GetValues("Content-Security-Policy").Single());
        Assert.True(res.Headers.CacheControl?.NoStore, "candidate and salary data must not be cached");
    }

    [Fact]
    public async Task Health_Probes_Carry_The_Headers_Too()
    {
        var res = await _factory.CreateClient().GetAsync("/health"); // HealthController
        Assert.Equal("nosniff", res.Headers.GetValues("X-Content-Type-Options").Single());
    }

    [Fact]
    public async Task Production_Allows_No_Cross_Origin_Callers()
    {
        var res = await Preflight(Production().CreateClient(), DevSpaOrigin);
        Assert.False(res.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Development_Allows_The_Dev_Servers()
    {
        var res = await Preflight(_factory.CreateClient(), DevSpaOrigin);
        Assert.Equal(DevSpaOrigin, res.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact]
    public async Task Development_Allows_No_Other_Origin()
    {
        var res = await Preflight(_factory.CreateClient(), "https://attacker.example");
        Assert.False(res.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Production_Responses_Carry_The_Headers()
    {
        var res = await Signed(Production()).GetAsync("/api/requisitions");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("DENY", res.Headers.GetValues("X-Frame-Options").Single());
    }
}
