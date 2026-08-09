using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RecruitOps.Api.Auth;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;

namespace RecruitOps.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous] // login/refresh/revoke must be reachable without an access token
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly ILoginThrottle _throttle;

    public AuthController(IAuthService auth, ILoginThrottle throttle)
    {
        _auth = auth;
        _throttle = throttle;
    }

    /// <summary>Exchanges email + password for a signed JWT + refresh token.
    /// <para>Rate-limited on two axes: per IP by the <see cref="RateLimitPolicies.Login"/>
    /// policy, and per account by <see cref="ILoginThrottle"/>.</para></summary>
    [HttpPost("login")]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var retryAfter = _throttle.RetryAfter(request.Email);
        if (retryAfter is not null)
        {
            Response.Headers.RetryAfter =
                ((int)Math.Ceiling(retryAfter.Value.TotalSeconds)).ToString(CultureInfo.InvariantCulture);

            return StatusCode(StatusCodes.Status429TooManyRequests, new ProblemDetails
            {
                Title = "Too many attempts",
                Detail = "Too many failed sign-in attempts. Try again shortly.",
            });
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _auth.LoginAsync(request, ipAddress, ct);

        if (result is null)
        {
            _throttle.RecordFailure(request.Email);
            return Unauthorized();
        }

        _throttle.Reset(request.Email);
        return Ok(result);
    }

    /// <summary>Exchanges a valid refresh token for a new access token and rotated refresh token.</summary>
    [HttpPost("refresh")]
    public async Task<ActionResult<LoginResponse>> Refresh([FromBody] RefreshRequest request, CancellationToken ct)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _auth.RefreshTokenAsync(request, ipAddress, ct);
        if (result is null)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Unauthorized",
                Detail = "Invalid, expired, or revoked refresh token."
            });
        }
        return Ok(result);
    }

    /// <summary>Revokes a refresh token (e.g. on user logout).</summary>
    [HttpPost("revoke")]
    public async Task<IActionResult> Revoke([FromBody] RefreshRequest request, CancellationToken ct)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _auth.RevokeTokenAsync(request.RefreshToken, ipAddress, ct);
        return NoContent();
    }
}
