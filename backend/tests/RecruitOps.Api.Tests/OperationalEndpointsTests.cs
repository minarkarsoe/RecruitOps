using System.Net;
using System.Net.Http.Json;
using RecruitOps.Api.Auth;
using RecruitOps.Application.DTOs;
using Xunit;

namespace RecruitOps.Api.Tests;

/// <summary>The endpoints an operator uses before anyone signs in (ADR-0004): liveness, readiness,
/// and the build identity the version registry is fed from.</summary>
public class OperationalEndpointsTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;

    public OperationalEndpointsTests(CustomWebAppFactory factory) => _factory = factory;

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Health_Endpoints_Answer_Anonymously_With_A_Bare_Status(string path)
    {
        var res = await _factory.CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        // A status word and nothing else: no version, no check names, no exception text.
        Assert.Equal("Healthy", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Version_Is_Not_Published_Anonymously()
    {
        var res = await _factory.CreateClient().GetAsync("/api/version");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Theory]
    [InlineData(Roles.Admin)]
    [InlineData(Roles.HiringManager)]
    public async Task Any_Signed_In_User_Can_Read_The_Version(string role)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Tenant", _factory.TenantA.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", role);

        var body = await client.GetFromJsonAsync<VersionDto>("/api/version");

        Assert.Equal("0.1.0", body!.Version);
        Assert.False(string.IsNullOrWhiteSpace(body.Environment));
    }
}
