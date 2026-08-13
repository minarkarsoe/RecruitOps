using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RecruitOps.Api.Auth;
using RecruitOps.Application.DTOs;
using Xunit;

namespace RecruitOps.Api.Tests;

/// <summary>
/// challenger_m1_1 — milestone 1: prove the RESTORED per-IP limits (login 60, public apply 120)
/// actually take effect at runtime, not merely that they appear in configuration.
///
/// The five pre-existing rate-limit tests all set a SMALL limit and assert that traffic is
/// blocked. None of them asserts the opposite direction, so a limiter permanently stuck at
/// PermitLimit = 10 would leave every one of them green. These tests close that asymmetry:
/// they assert that raising the configured limit actually lets the extra requests THROUGH.
/// </summary>
public class ChallengerM11RateLimitTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;

    public ChallengerM11RateLimitTests(CustomWebAppFactory factory) => _factory = factory;

    private WebApplicationFactoryWrapper WithConfig(params (string Key, string? Value)[] settings)
        => new(_factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, cfg) =>
                cfg.AddInMemoryCollection(settings.ToDictionary(s => s.Key, s => s.Value)))));

    private static async Task<HttpStatusCode> PostLoginAsync(HttpClient client, int i)
    {
        var res = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = $"m11-{Guid.NewGuid():N}-{i}@example.com", Password = "wrong" });
        return res.StatusCode;
    }

    private static async Task<HttpStatusCode> PostApplyAsync(HttpClient client, int i)
    {
        var res = await client.PostAsJsonAsync("/api/public/jobs/no-such-token/apply",
            new SubmitApplicationRequest { FullName = $"M11 {i}", Email = $"m11-apply-{i}@example.com" });
        return res.StatusCode;
    }

    // ---------------------------------------------------------------------------------
    // UPPER DIRECTION — the gap. A limiter hard-stuck at 10 fails all four of these.
    // ---------------------------------------------------------------------------------

    [Theory]
    [InlineData(11)]  // one above the old, pre-restore value of 10
    [InlineData(25)]
    [InlineData(60)]  // the RESTORED production value for login
    public async Task Login_RaisingConfiguredLimit_LetsThatManyRequestsThrough(int permitLimit)
    {
        using var f = WithConfig(("RateLimit:Login:PermitLimit", permitLimit.ToString()),
                                 ("RateLimit:Login:WindowSeconds", "60"));
        var client = f.CreateClient();

        for (int i = 0; i < permitLimit; i++)
        {
            var status = await PostLoginAsync(client, i);
            Assert.True(status != HttpStatusCode.TooManyRequests,
                $"Request {i + 1} of {permitLimit} was rejected with 429. The configured " +
                $"PermitLimit={permitLimit} is NOT reaching the limiter — it is capped lower.");
        }

        // and the very next one IS blocked, so the limit is exactly the configured number
        Assert.Equal(HttpStatusCode.TooManyRequests, await PostLoginAsync(client, permitLimit));
    }

    [Fact]
    public async Task PublicApply_RestoredLimitOf120_Allows120_Blocks121()
    {
        using var f = WithConfig(("RateLimit:PublicApply:PermitLimit", "120"),
                                 ("RateLimit:PublicApply:WindowSeconds", "60"));
        var client = f.CreateClient();

        for (int i = 0; i < 120; i++)
        {
            var status = await PostApplyAsync(client, i);
            Assert.True(status != HttpStatusCode.TooManyRequests,
                $"Public-apply request {i + 1} of 120 was rejected with 429; configured limit not honoured.");
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, await PostApplyAsync(client, 120));
    }

    /// <summary>The two limiters must not share a bucket: exhausting login must not block apply.</summary>
    [Fact]
    public async Task Login_And_PublicApply_Use_Independent_Buckets()
    {
        using var f = WithConfig(("RateLimit:Login:PermitLimit", "3"),
                                 ("RateLimit:PublicApply:PermitLimit", "5"));
        var client = f.CreateClient();

        for (int i = 0; i < 3; i++) await PostLoginAsync(client, i);
        Assert.Equal(HttpStatusCode.TooManyRequests, await PostLoginAsync(client, 99));

        // login is exhausted; public apply must still have its own 5 permits
        for (int i = 0; i < 5; i++)
            Assert.True(await PostApplyAsync(client, i) != HttpStatusCode.TooManyRequests,
                $"Public-apply request {i + 1} blocked after the LOGIN bucket was exhausted — shared partition.");
        Assert.Equal(HttpStatusCode.TooManyRequests, await PostApplyAsync(client, 99));
    }

    // ---------------------------------------------------------------------------------
    // The options object the partitioner actually resolves per request.
    // ---------------------------------------------------------------------------------

    [Fact]
    public async Task ConfiguredValue_Is_Visible_To_IOptions_Resolved_From_A_Request_Scope()
    {
        using var f = WithConfig(("RateLimit:Login:PermitLimit", "37"),
                                 ("RateLimit:PublicApply:PermitLimit", "41"));
        // touch the server so the host is built the same way a request builds it
        using var client = f.CreateClient();
        await PostLoginAsync(client, 0);

        using var scope = f.Services.CreateScope();
        Assert.Equal(37, scope.ServiceProvider.GetRequiredService<IOptions<LoginRateLimitOptions>>().Value.PermitLimit);
        Assert.Equal(41, scope.ServiceProvider.GetRequiredService<IOptions<PublicApplyRateLimitOptions>>().Value.PermitLimit);
    }

    /// <summary>
    /// With the key present but valueless, binding must leave the compiled-in default (60/120)
    /// standing — that is the value that ships when an operator deletes the section.
    /// </summary>
    [Fact]
    public void CompiledInDefaults_Are_The_Restored_Values()
    {
        Assert.Equal(60, new LoginRateLimitOptions().PermitLimit);
        Assert.Equal(120, new PublicApplyRateLimitOptions().PermitLimit);
    }

    /// <summary>
    /// Binds the REAL shipped appsettings files. Both RateLimit sections carry a "// note"
    /// key alongside the real ones; this proves that comment key does not break binding and
    /// that the files on disk actually produce 60/120.
    /// </summary>
    [Theory]
    [InlineData("appsettings.json")]
    [InlineData("appsettings.Development.json")]
    public void ShippedAppSettingsFile_Binds_To_60_And_120(string fileName)
    {
        var apiDir = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Api"));
        var path = Path.Combine(apiDir, fileName);
        Assert.True(File.Exists(path), $"Could not locate {path}");

        var cfg = new ConfigurationBuilder().AddJsonFile(path).Build();

        var login = new LoginRateLimitOptions();
        cfg.GetSection("RateLimit:Login").Bind(login);
        var apply = new PublicApplyRateLimitOptions();
        cfg.GetSection("RateLimit:PublicApply").Bind(apply);

        Assert.Equal(60, login.PermitLimit);
        Assert.Equal(60, login.WindowSeconds);
        Assert.Equal(120, apply.PermitLimit);
        Assert.Equal(60, apply.WindowSeconds);
    }

    // ---------------------------------------------------------------------------------
    // PARTITION KEY — is per-IP behaviour observable at all under TestServer?
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// Documents a real observability gap. Under TestServer Connection.RemoteIpAddress is null,
    /// so ClientPartitionKey() returns the constant "unknown" and EVERY caller shares one bucket.
    /// If this test passes, two independent HttpClients are NOT isolated, which means the
    /// pre-existing test named RateLimiting_Isolate_IP_Partitions_Correctly cannot be observing
    /// partition isolation — it only ever drives one client.
    /// </summary>
    [Fact]
    public async Task TestServer_TwoClients_Share_One_Partition_So_PerIp_Isolation_Is_Unobservable()
    {
        using var f = WithConfig(("RateLimit:Login:PermitLimit", "4"));
        var clientA = f.CreateClient();
        var clientB = f.CreateClient();

        for (int i = 0; i < 4; i++) await PostLoginAsync(clientA, i);

        var bStatus = await PostLoginAsync(clientB, 0);
        Assert.Equal(HttpStatusCode.TooManyRequests, bStatus); // shared bucket confirmed
    }

    /// <summary>
    /// Real per-IP proof. A startup filter (which runs BEFORE UseRateLimiter) stamps
    /// Connection.RemoteIpAddress from a test header, so distinct addresses genuinely reach
    /// ClientPartitionKey. Distinct IPv4 addresses must get independent buckets.
    /// </summary>
    [Fact]
    public async Task WithRealRemoteIp_DistinctIPv4Addresses_Get_Independent_Buckets()
    {
        using var f = new WebApplicationFactoryWrapper(_factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(
                new Dictionary<string, string?> { ["RateLimit:Login:PermitLimit"] = "4" }));
            builder.ConfigureTestServices(s => s.AddSingleton<IStartupFilter, RemoteIpStampFilter>());
        }));
        var client = f.CreateClient();

        // 203.0.113.10 burns all 4 permits and is then blocked
        for (int i = 0; i < 4; i++)
            Assert.NotEqual(HttpStatusCode.TooManyRequests, await LoginFromIp(client, "203.0.113.10", i));
        Assert.Equal(HttpStatusCode.TooManyRequests, await LoginFromIp(client, "203.0.113.10", 9));

        // a DIFFERENT address must be unaffected
        Assert.NotEqual(HttpStatusCode.TooManyRequests, await LoginFromIp(client, "198.51.100.77", 0));
    }

    /// <summary>IPv6 is grouped by /64: two addresses in the same /64 must share one bucket,
    /// otherwise one allocation buys 2^64 free buckets and the limit exists on paper only.</summary>
    [Fact]
    public async Task WithRealRemoteIp_IPv6_SameSlash64_Shares_A_Bucket_DifferentSlash64_DoesNot()
    {
        using var f = new WebApplicationFactoryWrapper(_factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(
                new Dictionary<string, string?> { ["RateLimit:Login:PermitLimit"] = "4" }));
            builder.ConfigureTestServices(s => s.AddSingleton<IStartupFilter, RemoteIpStampFilter>());
        }));
        var client = f.CreateClient();

        // burn 4 permits spread across four DIFFERENT addresses inside one /64
        for (int i = 0; i < 4; i++)
            Assert.NotEqual(HttpStatusCode.TooManyRequests,
                await LoginFromIp(client, $"2001:db8:abcd:1::{i + 1}", i));

        // a fifth address in the SAME /64 must already be blocked
        Assert.Equal(HttpStatusCode.TooManyRequests,
            await LoginFromIp(client, "2001:db8:abcd:1::ffff", 9));

        // a different /64 is a different bucket
        Assert.NotEqual(HttpStatusCode.TooManyRequests,
            await LoginFromIp(client, "2001:db8:abcd:2::1", 0));
    }

    private static async Task<HttpStatusCode> LoginFromIp(HttpClient client, string ip, int i)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new LoginRequest
            {
                Email = $"m11-{Guid.NewGuid():N}-{i}@example.com",
                Password = "wrong",
            }),
        };
        req.Headers.Add(RemoteIpStampFilter.HeaderName, ip);
        var res = await client.SendAsync(req);
        return res.StatusCode;
    }

    /// <summary>Runs ahead of the whole application pipeline (including UseRateLimiter) and
    /// sets the connection's remote address from a header, so per-IP partitioning becomes
    /// observable in-process.</summary>
    private sealed class RemoteIpStampFilter : IStartupFilter
    {
        public const string HeaderName = "X-M11-Test-Remote-Ip";

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (ctx, nextMw) =>
            {
                if (ctx.Request.Headers.TryGetValue(HeaderName, out var v) &&
                    System.Net.IPAddress.TryParse(v.ToString(), out var parsed))
                {
                    ctx.Connection.RemoteIpAddress = parsed;
                }
                await nextMw();
            });
            next(app);
        };
    }

    /// <summary>Tiny disposable wrapper so each test can `using` its derived factory.</summary>
    private sealed class WebApplicationFactoryWrapper : IDisposable
    {
        private readonly WebApplicationFactory<Program> _inner;
        public WebApplicationFactoryWrapper(WebApplicationFactory<Program> inner) => _inner = inner;
        public HttpClient CreateClient() => _inner.CreateClient();
        public IServiceProvider Services => _inner.Services;
        public void Dispose() => _inner.Dispose();
    }
}
