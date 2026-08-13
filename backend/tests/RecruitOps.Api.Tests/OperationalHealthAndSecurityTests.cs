using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using RecruitOps.Api.Controllers;
using RecruitOps.Application.DTOs;
using Xunit;

namespace RecruitOps.Api.Tests;

public class OperationalHealthAndSecurityTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;

    public OperationalHealthAndSecurityTests(CustomWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Get_Healthz_Returns_200_OK_With_Healthy_Status()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<HealthCheckResponse>();
        Assert.NotNull(content);
        Assert.Equal("Healthy", content!.Status);
    }

    [Fact]
    public async Task Get_Healthz_Is_Publicly_Accessible_Without_Authentication()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_Healthz_Returns_Valid_Health_Metrics_Format()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<HealthCheckResponse>();
        Assert.NotNull(content);
        Assert.False(string.IsNullOrWhiteSpace(content!.Uptime));
        Assert.True(content.UptimeSeconds >= 0);
        Assert.NotNull(content.Memory);
        Assert.True(content.Memory.AllocatedBytes > 0);
        Assert.True(content.Memory.WorkingSetBytes > 0);
        Assert.True(content.Memory.WorkingSetMB > 0);
        Assert.NotNull(content.Checks);
        Assert.True(content.Checks.ContainsKey("database"));
        Assert.True(content.Checks.ContainsKey("storage"));
    }

    [Fact]
    public async Task Get_Healthz_Database_And_Storage_Checks_Are_Healthy()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<HealthCheckResponse>();
        Assert.NotNull(content);
        Assert.Equal("Healthy", content!.Checks["database"].Status);
        Assert.Equal("Healthy", content.Checks["storage"].Status);
    }

    [Fact]
    public async Task Security_Headers_Are_Present_On_Healthz_Response()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("X-Content-Type-Options"));
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").First());
        Assert.True(response.Headers.Contains("X-Frame-Options"));
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").First());
        Assert.True(response.Headers.Contains("Referrer-Policy"));
        Assert.Equal("strict-origin-when-cross-origin", response.Headers.GetValues("Referrer-Policy").First());
        Assert.True(response.Headers.Contains("Content-Security-Policy"));
        Assert.Equal("default-src 'self'; frame-ancestors 'none'; object-src 'none';", response.Headers.GetValues("Content-Security-Policy").First());
    }

    [Fact]
    public async Task Security_Headers_Are_Present_On_Auth_Login_Response()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = "nobody@test.local", Password = "wrongpassword" });

        Assert.True(response.Headers.Contains("X-Content-Type-Options"));
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").First());
        Assert.True(response.Headers.Contains("X-Frame-Options"));
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").First());
        Assert.True(response.Headers.Contains("Referrer-Policy"));
        Assert.Equal("strict-origin-when-cross-origin", response.Headers.GetValues("Referrer-Policy").First());
        Assert.True(response.Headers.Contains("Content-Security-Policy"));
        Assert.Equal("default-src 'self'; frame-ancestors 'none'; object-src 'none';", response.Headers.GetValues("Content-Security-Policy").First());
    }

    [Fact]
    public async Task Security_Headers_Are_Present_On_404_NotFound_Response()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Tenant", _factory.TenantA.ToString());

        var response = await client.GetAsync("/api/nonexistent-route-9999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.True(response.Headers.Contains("X-Content-Type-Options"));
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").First());
        Assert.True(response.Headers.Contains("X-Frame-Options"));
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").First());
        Assert.True(response.Headers.Contains("Referrer-Policy"));
        Assert.Equal("strict-origin-when-cross-origin", response.Headers.GetValues("Referrer-Policy").First());
        Assert.True(response.Headers.Contains("Content-Security-Policy"));
        Assert.Equal("default-src 'self'; frame-ancestors 'none'; object-src 'none';", response.Headers.GetValues("Content-Security-Policy").First());
    }

    [Fact]
    public async Task Rate_Limiting_Middleware_Blocks_Excessive_Login_Requests_With_429()
    {
        using var customFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, cfg) =>
            {
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RateLimit:Login:PermitLimit"] = "10"
                });
            });
        });
        var client = customFactory.CreateClient();

        // 10 requests allowed
        for (int i = 0; i < 10; i++)
        {
            await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = $"test{i}@example.com", Password = "wrong" });
        }

        // 11th request should be rate limited (429)
        var response11 = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = "test11@example.com", Password = "wrong" });
        Assert.Equal(HttpStatusCode.TooManyRequests, response11.StatusCode);
    }

    [Fact]
    public async Task Rate_Limiting_Middleware_Blocks_Excessive_Public_Apply_Requests_With_429()
    {
        using var customFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, cfg) =>
            {
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RateLimit:PublicApply:PermitLimit"] = "10"
                });
            });
        });
        var client = customFactory.CreateClient();

        // 10 requests allowed
        for (int i = 0; i < 10; i++)
        {
            await client.PostAsJsonAsync("/api/public/jobs/test-token/apply", new SubmitApplicationRequest { FullName = $"Applicant {i}", Email = $"app{i}@example.com" });
        }

        // 11th request should be rate limited (429)
        var response11 = await client.PostAsJsonAsync("/api/public/jobs/test-token/apply", new SubmitApplicationRequest { FullName = "Applicant 11", Email = "app11@example.com" });
        Assert.Equal(HttpStatusCode.TooManyRequests, response11.StatusCode);
    }

    [Fact]
    public async Task Rate_Limiting_Response_Contains_RetryAfter_Header()
    {
        using var customFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, cfg) =>
            {
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RateLimit:Login:PermitLimit"] = "10"
                });
            });
        });
        var client = customFactory.CreateClient();

        for (int i = 0; i < 10; i++)
        {
            await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = $"retry{i}@example.com", Password = "wrong" });
        }

        var response11 = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = "retry11@example.com", Password = "wrong" });
        Assert.Equal(HttpStatusCode.TooManyRequests, response11.StatusCode);
        Assert.True(response11.Headers.Contains("Retry-After") || response11.Headers.RetryAfter != null);
    }
}
