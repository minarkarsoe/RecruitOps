using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RecruitOps.Api.Auth;
using RecruitOps.Application.DTOs;
using RecruitOps.Domain.Enums;
using RecruitOps.Infrastructure.Persistence;
using RecruitOps.Infrastructure.Services;
using Xunit;

namespace RecruitOps.Api.Tests;

/// <summary>ADR-0028. Found by the security review of the develop merge (B3): refresh tokens were
/// stored in plaintext, survived deactivation — and came back to life on reactivation — had no
/// rate limit, and never expired while in use.</summary>
public class RefreshTokenHardeningTests : IClassFixture<CustomWebAppFactory>
{
    private const string Password = "P@ssw0rd-Test-123";

    private readonly CustomWebAppFactory _factory;
    private readonly Module3Scenario _scenario;

    public RefreshTokenHardeningTests(CustomWebAppFactory factory)
    {
        _factory = factory;
        _scenario = new Module3Scenario(factory);
    }

    private HttpClient Admin() => _scenario.Client(Roles.Admin, _factory.AdminUserId);

    /// <summary>A user of their own, signed in for real.</summary>
    private async Task<(Guid UserId, LoginResponse Session)> SignedInUserAsync()
    {
        var email = $"session.{Guid.NewGuid():N}@alpha.test";
        var created = await Admin().PostAsJsonAsync("/api/users",
            new CreateUserRequest(email, "Session Holder", Password, null, "Recruiter"));
        created.EnsureSuccessStatusCode();
        var user = (await created.Content.ReadFromJsonAsync<UserDetailDto>())!;

        var login = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = email, Password = Password });
        login.EnsureSuccessStatusCode();
        return (user.Id, (await login.Content.ReadFromJsonAsync<LoginResponse>())!);
    }

    private Task<HttpResponseMessage> RefreshAsync(string token, HttpClient? client = null) =>
        (client ?? _factory.CreateClient()).PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(token));

    private async Task WithDbAsync(Func<AppDbContext, Task> work)
    {
        using var scope = _factory.Services.CreateScope();
        await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    [Fact]
    public async Task A_Refresh_Token_Is_Stored_Only_As_A_Hash()
    {
        var (userId, session) = await SignedInUserAsync();
        var token = session.RefreshToken!;

        await WithDbAsync(async db =>
        {
            var rows = await db.RefreshTokens.IgnoreQueryFilters().Where(r => r.UserId == userId).ToListAsync();
            var row = Assert.Single(rows);
            Assert.NotEqual(token, row.TokenHash);
            Assert.Equal(RefreshTokenHash.Of(token), row.TokenHash);
            Assert.Equal(64, row.TokenHash.Length); // SHA-256, hex
        });

        // And the hash is what is looked up: the token still works.
        Assert.Equal(HttpStatusCode.OK, (await RefreshAsync(token)).StatusCode);
    }

    [Fact]
    public async Task Deactivating_A_User_Ends_Their_Sessions_And_Reactivating_Does_Not_Revive_Them()
    {
        var (userId, session) = await SignedInUserAsync();

        (await Admin().PutAsync($"/api/users/{userId}/deactivate", null)).EnsureSuccessStatusCode();
        await WithDbAsync(async db =>
            Assert.All(await db.RefreshTokens.IgnoreQueryFilters().Where(r => r.UserId == userId).ToListAsync(),
                r => Assert.True(r.IsRevoked)));

        (await Admin().PutAsync($"/api/users/{userId}/reactivate", null)).EnsureSuccessStatusCode();

        // Before ADR-0028 this was 200: the token had only been refused while the user was inactive.
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(session.RefreshToken!)).StatusCode);

        // A browser refreshing a token that was ended for it is not a theft, and must not be
        // logged as one: only a token that was *rotated* and comes back is (smoke test, 2026-10-15).
        var log = await Admin().GetFromJsonAsync<PagedResult<AuditLogEntryDto>>($"/api/audit-log?targetId={userId}&pageSize=100");
        Assert.DoesNotContain(log!.Items, e => e.Action == AuditAction.RefreshTokenReused);
    }

    [Fact]
    public async Task A_Session_Ends_At_Its_Absolute_Lifetime_However_Active()
    {
        var (userId, session) = await SignedInUserAsync();
        await WithDbAsync(async db =>
        {
            var row = await db.RefreshTokens.IgnoreQueryFilters().SingleAsync(r => r.UserId == userId);
            row.SessionStartedAt = DateTimeOffset.UtcNow.AddDays(-31); // default cap: 30 days
            await db.SaveChangesAsync();
        });

        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(session.RefreshToken!)).StatusCode);
    }

    [Fact]
    public async Task A_Rotated_Token_Never_Outlives_Its_Session()
    {
        var (userId, session) = await SignedInUserAsync();
        var started = DateTimeOffset.UtcNow.AddDays(-25);
        await WithDbAsync(async db =>
        {
            var row = await db.RefreshTokens.IgnoreQueryFilters().SingleAsync(r => r.UserId == userId);
            row.SessionStartedAt = started;
            await db.SaveChangesAsync();
        });

        var res = await RefreshAsync(session.RefreshToken!);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var rotated = (await res.Content.ReadFromJsonAsync<LoginResponse>())!;

        // Five days left in the session, not fourteen — and the client is told so.
        Assert.True(rotated.RefreshTokenExpiresAtUtc <= started.AddDays(30).AddSeconds(5));
        await WithDbAsync(async db =>
        {
            var row = await db.RefreshTokens.IgnoreQueryFilters()
                .SingleAsync(r => r.TokenHash == RefreshTokenHash.Of(rotated.RefreshToken!));
            Assert.Equal(started, row.SessionStartedAt, TimeSpan.FromSeconds(1));
        });
    }

    [Fact]
    public async Task Reusing_A_Rotated_Token_Is_Recorded_In_The_Audit_Log()
    {
        var (userId, session) = await SignedInUserAsync();
        var rotatedRes = await RefreshAsync(session.RefreshToken!); // rotates
        rotatedRes.EnsureSuccessStatusCode();
        var rotated = (await rotatedRes.Content.ReadFromJsonAsync<LoginResponse>())!;

        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(session.RefreshToken!)).StatusCode);
        // Its successor was ended with it; using that is refused, but it is not a second theft.
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(rotated.RefreshToken!)).StatusCode);

        var log = await Admin().GetFromJsonAsync<PagedResult<AuditLogEntryDto>>($"/api/audit-log?targetId={userId}&pageSize=100");
        var entry = Assert.Single(log!.Items, e => e.Action == AuditAction.RefreshTokenReused);
        Assert.Contains("1 active session", entry.Summary);
        Assert.True(entry.IsRefusal);
        Assert.DoesNotContain(session.RefreshToken!, entry.Summary + entry.Details?.GetRawText());
    }

    [Fact]
    public async Task Refresh_And_Revoke_Are_Rate_Limited_Per_Client()
    {
        using var limited = _factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, cfg) =>
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimit:Refresh:PermitLimit"] = "2",
                ["RateLimit:Refresh:WindowSeconds"] = "60",
            })));
        var client = limited.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync("not-a-token-1", client)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync("not-a-token-2", client)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await RefreshAsync("not-a-token-3", client)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests,
            (await client.PostAsJsonAsync("/api/auth/revoke", new RefreshRequest("not-a-token-4"))).StatusCode);
    }
}
