using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RecruitOps.Application.DTOs;
using RecruitOps.Domain.Entities;
using RecruitOps.Infrastructure.Persistence;
using Xunit;

namespace RecruitOps.Api.Tests;

public class AuthRefreshTokenTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;

    public AuthRefreshTokenTests(CustomWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Login_Returns_Valid_RefreshToken_And_Persists_In_Database()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = CustomWebAppFactory.AdminEmail, Password = CustomWebAppFactory.AdminPassword });

        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<LoginResponse>();

        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(body.RefreshToken));
        Assert.True(body.RefreshTokenExpiresAtUtc > DateTimeOffset.UtcNow);

        // Verify token entity was persisted in DB
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tokenEntity = await db.RefreshTokens
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Token == body.RefreshToken);

        Assert.NotNull(tokenEntity);
        Assert.Equal(body.UserId, tokenEntity!.UserId);
        Assert.False(tokenEntity.IsRevoked);
        Assert.False(tokenEntity.IsExpired);
    }

    [Fact]
    public async Task RefreshToken_ValidToken_ReturnsNewTokenPair_And_Rotates_Token()
    {
        var client = _factory.CreateClient();
        var loginRes = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = CustomWebAppFactory.AdminEmail, Password = CustomWebAppFactory.AdminPassword });

        loginRes.EnsureSuccessStatusCode();
        var loginBody = await loginRes.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(loginBody);

        var refreshRes = await client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest(loginBody!.RefreshToken!));

        refreshRes.EnsureSuccessStatusCode();
        var refreshBody = await refreshRes.Content.ReadFromJsonAsync<LoginResponse>();

        Assert.NotNull(refreshBody);
        Assert.False(string.IsNullOrWhiteSpace(refreshBody!.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(refreshBody.RefreshToken));
        Assert.NotEqual(loginBody.AccessToken, refreshBody.AccessToken);
        Assert.NotEqual(loginBody.RefreshToken, refreshBody.RefreshToken);

        // Verify old token was marked revoked and replacedByToken set
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var oldTokenEntity = await db.RefreshTokens
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Token == loginBody.RefreshToken);

        Assert.NotNull(oldTokenEntity);
        Assert.True(oldTokenEntity!.IsRevoked);
        Assert.Equal(refreshBody.RefreshToken, oldTokenEntity.ReplacedByToken);

        // Verify new token entity exists and is active
        var newTokenEntity = await db.RefreshTokens
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Token == refreshBody.RefreshToken);

        Assert.NotNull(newTokenEntity);
        Assert.False(newTokenEntity!.IsRevoked);
    }

    [Fact]
    public async Task RefreshToken_ExpiredToken_Returns401()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var user = await db.Users.IgnoreQueryFilters().FirstAsync(u => u.Email == CustomWebAppFactory.AdminEmail);
        var expiredTokenStr = "expired_refresh_token_" + Guid.NewGuid().ToString("N");

        db.RefreshTokens.Add(new RefreshToken
        {
            TenantId = user.TenantId,
            UserId = user.Id,
            Token = expiredTokenStr,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1), // Expired 1 hour ago
            IsRevoked = false
        });
        await db.SaveChangesAsync();

        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(expiredTokenStr));

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task RefreshToken_RevokedToken_Returns401()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var user = await db.Users.IgnoreQueryFilters().FirstAsync(u => u.Email == CustomWebAppFactory.AdminEmail);
        var revokedTokenStr = "revoked_refresh_token_" + Guid.NewGuid().ToString("N");

        db.RefreshTokens.Add(new RefreshToken
        {
            TenantId = user.TenantId,
            UserId = user.Id,
            Token = revokedTokenStr,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            IsRevoked = true,
            RevokedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(revokedTokenStr));

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task RefreshToken_ReuseDetection_RevokesAllUserTokens()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var user = await db.Users.IgnoreQueryFilters().FirstAsync(u => u.Email == CustomWebAppFactory.AdminEmail);

        var stolenRevokedTokenStr = "stolen_revoked_token_" + Guid.NewGuid().ToString("N");
        var activeToken1Str = "active_token_1_" + Guid.NewGuid().ToString("N");
        var activeToken2Str = "active_token_2_" + Guid.NewGuid().ToString("N");

        db.RefreshTokens.Add(new RefreshToken
        {
            TenantId = user.TenantId,
            UserId = user.Id,
            Token = stolenRevokedTokenStr,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            IsRevoked = true,
            RevokedAt = DateTimeOffset.UtcNow
        });

        db.RefreshTokens.Add(new RefreshToken
        {
            TenantId = user.TenantId,
            UserId = user.Id,
            Token = activeToken1Str,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            IsRevoked = false
        });

        db.RefreshTokens.Add(new RefreshToken
        {
            TenantId = user.TenantId,
            UserId = user.Id,
            Token = activeToken2Str,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            IsRevoked = false
        });

        await db.SaveChangesAsync();

        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(stolenRevokedTokenStr));

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);

        // Verify ALL active tokens for user are now revoked
        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();

        var activeTokensCount = await verifyDb.RefreshTokens
            .IgnoreQueryFilters()
            .Where(r => r.UserId == user.Id && !r.IsRevoked)
            .CountAsync();

        Assert.Equal(0, activeTokensCount);
    }

    [Fact]
    public async Task RevokeToken_ExplicitLogout_RevokesToken()
    {
        var client = _factory.CreateClient();
        var loginRes = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = CustomWebAppFactory.AdminEmail, Password = CustomWebAppFactory.AdminPassword });

        loginRes.EnsureSuccessStatusCode();
        var loginBody = await loginRes.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(loginBody);

        var revokeRes = await client.PostAsJsonAsync("/api/auth/revoke",
            new RefreshRequest(loginBody!.RefreshToken!));

        Assert.Equal(HttpStatusCode.NoContent, revokeRes.StatusCode);

        // Verify token entity is revoked in database
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tokenEntity = await db.RefreshTokens
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Token == loginBody.RefreshToken);

        Assert.NotNull(tokenEntity);
        Assert.True(tokenEntity!.IsRevoked);
        Assert.NotNull(tokenEntity.RevokedAt);

        // Subsequent refresh attempt with revoked token should fail with 401
        var refreshRes = await client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest(loginBody.RefreshToken!));

        Assert.Equal(HttpStatusCode.Unauthorized, refreshRes.StatusCode);
    }
}
