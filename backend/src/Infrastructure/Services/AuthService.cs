using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;
using RecruitOps.Domain.Entities;
using RecruitOps.Domain.Enums;
using RecruitOps.Infrastructure.Persistence;

namespace RecruitOps.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher<User> _hasher;
    private readonly ITokenService _tokens;
    private readonly IPermissionEvaluator _permissions;
    private readonly IAuditTrail _audit;

    public AuthService(
        AppDbContext db,
        IPasswordHasher<User> hasher,
        ITokenService tokens,
        IPermissionEvaluator permissions,
        IAuditTrail audit)
    {
        _audit = audit;
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
        // Case-insensitive and trimmed: the directory stores emails lowercased, so an exact match
        // turned "Aung@Corp.com" into a wrong-password error (second security review, 2026-10-15).
        // ToLower() on the column also matches rows written before emails were normalised.
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email && u.IsActive, ct);

        if (user is null)
        {
            var (dummyUser, dummyHash) = DummyCredential.Value;
            _hasher.VerifyHashedPassword(dummyUser, dummyHash, request.Password);
            await RecordFailedSignInAsync(email, ct);
            return null;
        }

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
        {
            // The actor is whoever typed the password — not necessarily the account holder — so the
            // entry is about the account (target) and attributed to the email tried, not the user.
            await _audit.RecordStandaloneAsync(AuditAction.SignInFailed, AuditTarget.User(user.Id, user.DisplayName),
                $"Sign-in failed for {user.DisplayName}: wrong password.",
                new { email, reason = "wrong_password" },
                actor: new AuditActor(null, email), tenantId: user.TenantId, ct: ct);
            return null;
        }

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
        await _audit.RecordAsync(AuditAction.SignInSucceeded, AuditTarget.User(user.Id, user.DisplayName),
            $"{user.DisplayName} signed in.", new { email, role = user.Role.ToString() },
            actor: new AuditActor(user.Id, user.DisplayName), tenantId: user.TenantId, ct: ct);
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
            permissions.Order(StringComparer.Ordinal).ToArray());
    }

    /// <summary>No active account matched. Says whether the account exists but is deactivated —
    /// the auditor needs that; the caller only ever gets 401 (ADR-0027). The password is never
    /// recorded, not even a wrong one: it is often a real password one character off.</summary>
    private async Task RecordFailedSignInAsync(string email, CancellationToken ct)
    {
        var known = await _db.Users.AsNoTracking().IgnoreQueryFilters()
            .Where(u => u.Email.ToLower() == email)
            .Select(u => new { u.Id, u.DisplayName, u.TenantId })
            .FirstOrDefaultAsync(ct);

        if (known is null)
        {
            await _audit.RecordStandaloneAsync(AuditAction.SignInFailed, null,
                $"Sign-in failed for {email}: no account with this email.",
                new { email, reason = "no_account" }, actor: new AuditActor(null, email), ct: ct);
            return;
        }

        await _audit.RecordStandaloneAsync(AuditAction.SignInFailed, AuditTarget.User(known.Id, known.DisplayName),
            $"Sign-in failed for {known.DisplayName}: the account is deactivated.",
            new { email, reason = "deactivated" }, actor: new AuditActor(null, email), tenantId: known.TenantId, ct: ct);
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
            permissions.Order(StringComparer.Ordinal).ToArray());
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
