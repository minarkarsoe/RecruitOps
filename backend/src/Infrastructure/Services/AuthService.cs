using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;
using RecruitOps.Domain.Entities;
using RecruitOps.Infrastructure.Persistence;

namespace RecruitOps.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher<User> _hasher;
    private readonly ITokenService _tokens;
    private readonly IPermissionEvaluator _permissions;

    public AuthService(
        AppDbContext db,
        IPasswordHasher<User> hasher,
        ITokenService tokens,
        IPermissionEvaluator permissions)
    {
        _db = db;
        _hasher = hasher;
        _tokens = tokens;
        _permissions = permissions;
    }

    /// <summary>A real hash of a password nobody has, verified against when the account does
    /// not exist. Returning early on an unknown email would make the response measurably
    /// faster — PBKDF2 is deliberately slow — turning response time into a user-enumeration
    /// oracle and undoing the identical-401 behaviour below. Built once, statically, so the
    /// hashing cost lands on the attacker's request rather than on startup of every request.</summary>
    private static readonly Lazy<(User User, string Hash)> DummyCredential = new(() =>
    {
        var user = new User { Email = "no-such-user@invalid", DisplayName = "-" };
        var hash = new PasswordHasher<User>().HashPassword(user, Guid.NewGuid().ToString());
        return (user, hash);
    });

    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        return await LoginAsync(request, ipAddress: null, ct);
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request, string? ipAddress = null, CancellationToken ct = default)
    {
        // Pre-authentication: there is no tenant context yet, so bypass the tenant
        // query filter to find the user.
        var user = await _db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == request.Email && u.IsActive, ct);

        if (user is null)
        {
            var (dummyUser, dummyHash) = DummyCredential.Value;
            _hasher.VerifyHashedPassword(dummyUser, dummyHash, request.Password);
            return null;
        }

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
            return null;

        var tokenResult = _tokens.CreateTokens(user);

        var refreshTokenEntity = new RefreshToken
        {
            TenantId = user.TenantId,
            UserId = user.Id,
            Token = tokenResult.RefreshToken,
            ExpiresAt = tokenResult.RefreshTokenExpiresAtUtc,
            CreatedByIp = ipAddress,
            IsRevoked = false
        };

        _db.RefreshTokens.Add(refreshTokenEntity);
        await _db.SaveChangesAsync(ct);

        var permissions = await _permissions.GetUserPermissionsAsync(user.Id, user.TenantId, ct);

        return new LoginResponse(
            tokenResult.AccessToken,
            tokenResult.ExpiresAtUtc,
            tokenResult.RefreshToken,
            tokenResult.RefreshTokenExpiresAtUtc,
            user.Role.ToString(),
            user.DisplayName,
            user.Id,
            permissions.ToArray());
    }

    public async Task<LoginResponse?> RefreshTokenAsync(RefreshRequest request, string? ipAddress = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return null;

        var existingToken = await _db.RefreshTokens
            .IgnoreQueryFilters()
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Token == request.RefreshToken, ct);

        if (existingToken is null)
            return null;

        // Reuse detection: if token is already revoked, revoke all active refresh tokens for the user
        if (existingToken.IsRevoked)
        {
            var activeTokens = await _db.RefreshTokens
                .IgnoreQueryFilters()
                .Where(r => r.UserId == existingToken.UserId && !r.IsRevoked)
                .ToListAsync(ct);

            foreach (var t in activeTokens)
            {
                t.IsRevoked = true;
                t.RevokedAt = DateTimeOffset.UtcNow;
                t.RevokedByIp = ipAddress;
            }

            await _db.SaveChangesAsync(ct);
            return null;
        }

        if (existingToken.IsExpired || existingToken.User is null || !existingToken.User.IsActive)
            return null;

        // Token rotation: revoke current token and issue new token pair
        var newTokenResult = _tokens.CreateTokens(existingToken.User);

        existingToken.IsRevoked = true;
        existingToken.RevokedAt = DateTimeOffset.UtcNow;
        existingToken.RevokedByIp = ipAddress;
        existingToken.ReplacedByToken = newTokenResult.RefreshToken;

        var newRefreshTokenEntity = new RefreshToken
        {
            TenantId = existingToken.User.TenantId,
            UserId = existingToken.User.Id,
            Token = newTokenResult.RefreshToken,
            ExpiresAt = newTokenResult.RefreshTokenExpiresAtUtc,
            CreatedByIp = ipAddress,
            IsRevoked = false
        };

        _db.RefreshTokens.Add(newRefreshTokenEntity);
        await _db.SaveChangesAsync(ct);

        var permissions = await _permissions.GetUserPermissionsAsync(existingToken.User.Id, existingToken.User.TenantId, ct);

        return new LoginResponse(
            newTokenResult.AccessToken,
            newTokenResult.ExpiresAtUtc,
            newTokenResult.RefreshToken,
            newTokenResult.RefreshTokenExpiresAtUtc,
            existingToken.User.Role.ToString(),
            existingToken.User.DisplayName,
            existingToken.User.Id,
            permissions.ToArray());
    }

    public async Task<bool> RevokeTokenAsync(string refreshToken, string? ipAddress = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return false;

        var token = await _db.RefreshTokens
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Token == refreshToken, ct);

        if (token is null)
            return false;

        if (!token.IsRevoked)
        {
            token.IsRevoked = true;
            token.RevokedAt = DateTimeOffset.UtcNow;
            token.RevokedByIp = ipAddress;
            await _db.SaveChangesAsync(ct);
        }

        return true;
    }
}
