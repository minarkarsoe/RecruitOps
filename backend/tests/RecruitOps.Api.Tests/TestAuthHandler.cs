using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RecruitOps.Api.Auth;

namespace RecruitOps.Api.Tests;

/// <summary>Test authentication scheme: injects tenant + role claims from request
/// headers so tests exercise the real authorization + tenant-filter pipeline
/// without minting real JWTs. Missing tenant header => 401 (as [Authorize] expects).</summary>
public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    /// <summary>Scheme name. Deliberately not called "Scheme" — the base
    /// AuthenticationHandler already exposes a Scheme property, and shadowing it
    /// produces CS0108 and confusing resolution.</summary>
    public const string SchemeName = "Test";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-Tenant", out var tenant))
            return Task.FromResult(AuthenticateResult.NoResult());

        var claims = new List<Claim> { new(AppClaims.TenantId, tenant.ToString()) };

        // Department scoping (ADR-0003) resolves the user from this claim.
        if (Request.Headers.TryGetValue("X-Test-UserId", out var userId))
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.ToString()));
        if (Request.Headers.TryGetValue("X-Test-Roles", out var roles))
            foreach (var r in roles.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries))
                claims.Add(new Claim(ClaimTypes.Role, r.Trim()));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
