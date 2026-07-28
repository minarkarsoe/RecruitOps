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

    public AuthService(AppDbContext db, IPasswordHasher<User> hasher, ITokenService tokens)
    {
        _db = db;
        _hasher = hasher;
        _tokens = tokens;
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
        // Pre-authentication: there is no tenant context yet, so bypass the tenant
        // query filter to find the user. (Email is treated as unique per user for now;
        // multi-tenant same-email login needs a tenant selector — see TODO in docs.)
        var user = await _db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == request.Email && u.IsActive, ct);

        if (user is null)
        {
            // Burn the same work an existing account would have cost. The result is
            // discarded; only the elapsed time matters.
            var (dummyUser, dummyHash) = DummyCredential.Value;
            _hasher.VerifyHashedPassword(dummyUser, dummyHash, request.Password);
            return null;
        }

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
            return null;

        var token = _tokens.CreateToken(user);
        return new LoginResponse(token.AccessToken, token.ExpiresAtUtc, user.Role.ToString(), user.DisplayName, user.Id);
    }
}
