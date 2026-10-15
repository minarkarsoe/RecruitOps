using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using RecruitOps.Api.Auth;
using Xunit;

namespace RecruitOps.Api.Tests;

/// <summary>An unhandled exception reaches the caller as problem details with a trace id — and
/// never as the exception's own text. Run as Production, the only environment where it applies:
/// Development keeps the developer exception page.</summary>
public class ErrorHandlingTests : IClassFixture<CustomWebAppFactory>
{
    private const string Secret = "connection to db-prod-07.internal failed for user hr_admin";

    private readonly HttpClient _client;

    public ErrorHandlingTests(CustomWebAppFactory factory)
    {
        _client = factory.WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Production");
            b.ConfigureTestServices(s => s.AddSingleton<IStartupFilter, ThrowingRouteFilter>());
        }).CreateClient();

        _client.DefaultRequestHeaders.Add("X-Test-Tenant", factory.TenantA.ToString());
        _client.DefaultRequestHeaders.Add("X-Test-Roles", Roles.Admin);
    }

    [Fact]
    public async Task An_Unhandled_Exception_Becomes_Problem_Details_Without_Its_Message()
    {
        var res = await _client.GetAsync("/__throw");

        Assert.Equal(HttpStatusCode.InternalServerError, res.StatusCode);
        Assert.Equal("application/problem+json", res.Content.Headers.ContentType?.MediaType);

        var body = await res.Content.ReadAsStringAsync();
        Assert.DoesNotContain(Secret, body);
        Assert.DoesNotContain("InvalidOperationException", body);
        Assert.DoesNotContain(" at ", body); // no stack frames

        using var problem = JsonDocument.Parse(body);
        Assert.Equal(500, problem.RootElement.GetProperty("status").GetInt32());
        // The handle that links a user's report to the server log.
        Assert.False(string.IsNullOrWhiteSpace(problem.RootElement.GetProperty("traceId").GetString()));
    }

    /// <summary>Security review of the develop merge, A6: the exception handler clears the response
    /// — headers included — before writing the 500, so headers set on the way in were lost.</summary>
    [Fact]
    public async Task An_Error_Response_Keeps_The_Security_Headers()
    {
        var res = await _client.GetAsync("/__throw");

        Assert.Equal(HttpStatusCode.InternalServerError, res.StatusCode);
        Assert.Equal("nosniff", res.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", res.Headers.GetValues("X-Frame-Options").Single());
        Assert.Contains("frame-ancestors 'none'", res.Headers.GetValues("Content-Security-Policy").Single());
    }

    /// <summary>Adds a route that throws, behind the app's own middleware — so the exception
    /// travels back out through exactly the pipeline a real one would.</summary>
    private sealed class ThrowingRouteFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            next(app);
            app.Map("/__throw", branch => branch.Run(_ => throw new InvalidOperationException(Secret)));
        };
    }
}
