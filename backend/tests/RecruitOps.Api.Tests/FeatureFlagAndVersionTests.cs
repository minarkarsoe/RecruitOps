using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using RecruitOps.Api.Controllers;
using Xunit;

namespace RecruitOps.Api.Tests;

public class FeatureFlagAndVersionTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;

    public FeatureFlagAndVersionTests(CustomWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Get_Version_Returns_200_OK_With_Metadata_And_FeatureFlags()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/version");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<VersionInfoDto>();
        Assert.NotNull(content);
        Assert.False(string.IsNullOrWhiteSpace(content!.Version));
        Assert.False(string.IsNullOrWhiteSpace(content.Environment));
        Assert.NotNull(content.FeatureFlags);
        Assert.True(content.FeatureFlags.ContainsKey("EnableAnalytics"));
    }

    [Fact]
    public async Task Get_Health_Returns_200_OK_Alias_Endpoint()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<HealthCheckResponse>();
        Assert.NotNull(content);
        Assert.Equal("Healthy", content!.Status);
    }

    [Fact]
    public async Task Disabled_Feature_Returns_403_Forbidden_With_Error_Payload()
    {
        // Factory with EnableAnalytics disabled
        var disabledFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((ctx, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["FeatureFlags:EnableAnalytics"] = "false"
                });
            });
        });

        var client = disabledFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Tenant", _factory.TenantA.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", "Admin");

        var response = await client.GetAsync("/api/analytics/kpis");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("FeatureDisabled", json);
        Assert.Contains("EnableAnalytics", json);
    }
}
