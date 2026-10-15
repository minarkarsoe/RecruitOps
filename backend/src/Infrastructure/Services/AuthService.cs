using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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
    private readonly UnknownSignInAuditGate _unknownGate;
    private readonly IConfiguration _config;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        AppDbContext db,
        IPasswordHasher<User> hasher,
        ITokenService tokens,
        IPermissionEvaluator permissions,
        IAuditTrail audit,
        UnknownSignInAuditGate unknownGate,
        IConfiguration config,
        ILogger<AuthService> logger)
    {
        _audit = audit;
        _unknownGate = unknownGate;
        _config = config;
        _logger = logger;
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
        //
        // One lookup, active or not: the audit log needs to know a deactivated account from a
        // missing one, and a second query on only one of the two failure paths would make them
        // distinguishable by response time (security review of ADR-0027, L2).
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email, ct);

        if (user is null || !user.IsActive)
        {
            var (dummyUser, dummyHash) = DummyCredential.Value;
            _hasher.VerifyHashedPassword(dummyUser, dummyHash, request.Password);

            if (user is null)
                await RecordUnknownSignInAsync(email, ipAddress);
            else
                await RecordFailedSignInAsync(user, "the account is deactivated", "deactivated");
            return null;
        }

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
        {
            await RecordFailedSignInAsync(user, "wrong password", "wrong_password");
            return null;
        }

        var tokenResult = _tokens.CreateTokens(user);
        var now = DateTimeOffset.UtcNow;

        var refreshTokenEntity = new RefreshToken
        {
            TenantId = user.TenantId,
            UserId = user.Id,
            TokenHash = RefreshTokenHash.Of(tokenResult.RefreshToken),
            SessionStartedAt = now,
            ExpiresAt = Cap(tokenResult.RefreshTokenExpiresAtUtc, now),
            CreatedByIp = ipAddress,
            IsRevoked = false
        };

        _db.RefreshTokens.Add(refreshTokenEntity);
        await _audit.RecordAsync(AuditAction.SignInSucceeded, AuditTarget.User(user.Id, user.DisplayName),
            $"{user.DisplayName} signed in.", new { email = user.Email, role = user.Role.ToString() },
            actor: new AuditActor(user.Id, user.DisplayName), tenantId: user.TenantId, ct: ct);
        await _db.SaveChangesAsync(ct);

        var permissions = await _permissions.GetUserPermissionsAsync(user.Id, user.TenantId, ct);

        return new LoginResponse(
            tokenResult.AccessToken,
            tokenResult.ExpiresAtUtc,
            tokenResult.RefreshToken,
            refreshTokenEntity.ExpiresAt,
            user.Role.ToString(),
            user.DisplayName,
            user.Id,
            permissions.Order(StringComparer.Ordinal).ToArray());
    }

    /// <summary>A real account. The actor is whoever typed the password — not necessarily its
    /// holder — so the entry is about the account and attributed to no one. The password is never
    /// recorded, not even a wrong one: it is often a real password one character off.</summary>
    private Task RecordFailedSignInAsync(User user, string why, string reason) =>
        RecordSafelyAsync(AuditAction.SignInFailed, AuditTarget.User(user.Id, user.DisplayName),
            $"Sign-in failed for {user.DisplayName}: {why}.",
            new { email = user.Email, reason },
            new AuditActor(null, user.Email), user.TenantId);

    /// <summary>No account has this email. Gated (<see cref="UnknownSignInAuditGate"/>), and the
    /// text typed is kept only if it is a plain email address (security review of ADR-0027, M2):
    /// the field takes anything with an "@", and people paste passwords into the wrong box.</summary>
    private async Task RecordUnknownSignInAsync(string typed, string? ipAddress)
    {
        var (record, suppressedBefore) = _unknownGate.Admit(ipAddress);
        if (!record) return;

        var identifier = UnknownSignInAuditGate.PlainEmailOrNull(typed);
        var shown = identifier ?? "a sign-in name that is not an email address";
        var earlier = suppressedBefore > 0
            ? $" {suppressedBefore} earlier such attempt{(suppressedBefore == 1 ? "" : "s")} from this address were not recorded one by one."
            : string.Empty;

        await RecordSafelyAsync(AuditAction.SignInFailed, null,
            $"Sign-in failed for {shown}: no account with this email.{earlier}",
            new { email = identifier, reason = "no_account", earlierAttemptsNotRecorded = suppressedBefore },
            new AuditActor(null, identifier ?? "(not an email address)"), tenantId: null);
    }

    /// <summary>A failed sign-in is still a 401, and still counts against the throttle, if the audit
    /// write fails — and it is not abandoned when the client disconnects (security review of
    /// ADR-0027, L1). Losing an entry is logged loudly instead.</summary>
    private async Task RecordSafelyAsync(
        AuditAction action, AuditTarget? target, string summary, object details, AuditActor actor, Guid? tenantId)
    {
        try
        {
            await _audit.RecordStandaloneAsync(action, target, summary, details, actor, tenantId, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not write audit entry {Action}: {Summary}", action, summary);
        }
    }

    /// <summary>A refresh token never outlives its session (ADR-0028).</summary>
    private DateTimeOffset Cap(DateTimeOffset expiresAt, DateTimeOffset sessionStartedAt) =>
        Min(expiresAt, sessionStartedAt + SessionMax);

    private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a < b ? a : b;

    /// <summary><c>Jwt:SessionMaxDays</c>, default 30: how long one sign-in can be kept alive by
    /// rotation (ADR-0028).</summary>
    private TimeSpan SessionMax =>
        TimeSpan.FromDays(_config.GetValue<int?>("Jwt:SessionMaxDays") is { } days and > 0 ? days : 30);

    public async Task<LoginResponse?> RefreshTokenAsync(RefreshRequest request, string? ipAddress = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return null;

        var presentedHash = RefreshTokenHash.Of(request.RefreshToken);
        var existingToken = await _db.RefreshTokens
            .IgnoreQueryFilters()
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.TokenHash == presentedHash, ct);

        if (existingToken is null)
            return null;

        var now = DateTimeOffset.UtcNow;

        // A token ended for its holder — signed out, deactivated, or swept up by the reuse response
        // below — is simply refused. A browser refreshing one is not an attack, and calling it one
        // trains auditors to ignore the alarm (found by smoke-testing ADR-0028).
        if (existingToken.IsRevoked && existingToken.ReplacedByTokenHash is null)
            return null;

        // Reuse detection: a *rotated* token presented again means someone else has a copy — the
        // legitimate client already traded it for its successor. Revoke every session the user
        // has, and say so in the audit log: it is the one signal that a token was stolen (ADR-0028).
        if (existingToken.IsRevoked)
        {
            var activeTokens = await _db.RefreshTokens
                .IgnoreQueryFilters()
                .Where(r => r.UserId == existingToken.UserId && !r.IsRevoked)
                .ToListAsync(ct);

            foreach (var t in activeTokens)
            {
                t.IsRevoked = true;
                t.RevokedAt = now;
                t.RevokedByIp = ipAddress;
            }

            if (existingToken.User is { } owner)
                await _audit.RecordAsync(AuditAction.RefreshTokenReused, AuditTarget.User(owner.Id, owner.DisplayName),
                    $"A revoked session token for {owner.DisplayName} was used again — {activeTokens.Count} active session{(activeTokens.Count == 1 ? " was" : "s were")} ended.",
                    new { sessionsEnded = activeTokens.Count, tokenRevokedAt = existingToken.RevokedAt },
                    actor: new AuditActor(null, null), tenantId: owner.TenantId, ct: ct);

            await _db.SaveChangesAsync(ct);
            return null;
        }

        if (existingToken.IsExpired || existingToken.User is null || !existingToken.User.IsActive)
            return null;

        // ADR-0028: however active, a session ends SessionMax after its sign-in.
        if (now >= existingToken.SessionStartedAt + SessionMax)
            return null;

        // Token rotation: revoke current token and issue new token pair
        var newTokenResult = _tokens.CreateTokens(existingToken.User);
        var newHash = RefreshTokenHash.Of(newTokenResult.RefreshToken);

        existingToken.IsRevoked = true;
        existingToken.RevokedAt = now;
        existingToken.RevokedByIp = ipAddress;
        existingToken.ReplacedByTokenHash = newHash;

        var newRefreshTokenEntity = new RefreshToken
        {
            TenantId = existingToken.User.TenantId,
            UserId = existingToken.User.Id,
            TokenHash = newHash,
            SessionStartedAt = existingToken.SessionStartedAt,
            ExpiresAt = Cap(newTokenResult.RefreshTokenExpiresAtUtc, existingToken.SessionStartedAt),
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
            newRefreshTokenEntity.ExpiresAt,
            existingToken.User.Role.ToString(),
            existingToken.User.DisplayName,
            existingToken.User.Id,
            permissions.Order(StringComparer.Ordinal).ToArray());
    }

    public async Task<bool> RevokeTokenAsync(string refreshToken, string? ipAddress = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return false;

        var hash = RefreshTokenHash.Of(refreshToken);
        var token = await _db.RefreshTokens
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.TokenHash == hash, ct);

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
