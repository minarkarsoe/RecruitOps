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
[AllowAnonymous] // login must be reachable without a token
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly ILoginThrottle _throttle;

    public AuthController(IAuthService auth, ILoginThrottle throttle)
    {
        _auth = auth;
        _throttle = throttle;
    }

    /// <summary>Exchanges email + password for a signed JWT.
    /// <para>Rate-limited on two axes: per IP by the <see cref="RateLimitPolicies.Login"/>
    /// policy, and per account by <see cref="ILoginThrottle"/>. Without an anonymous
    /// endpoint that verifies a secret being limited, the password policy is the only thing
    /// standing between an attacker and every account.</para></summary>
    [HttpPost("login")]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var retryAfter = _throttle.RetryAfter(request.Email);
        if (retryAfter is not null)
        {
            Response.Headers.RetryAfter =
                ((int)Math.Ceiling(retryAfter.Value.TotalSeconds)).ToString(CultureInfo.InvariantCulture);

            // Note this is returned for unknown emails too. Only locking out real accounts
            // would make the 429 an existence oracle, undoing the enumeration protection
            // the matching 401s below are there to provide.
            return StatusCode(StatusCodes.Status429TooManyRequests, new ProblemDetails
            {
                Title = "Too many attempts",
                Detail = "Too many failed sign-in attempts. Try again shortly.",
            });
        }

        var result = await _auth.LoginAsync(request, ct);

        if (result is null)
        {
            _throttle.RecordFailure(request.Email);
            // Same 401 for unknown user and bad password — don't reveal which.
            return Unauthorized();
        }

        // A user who mistypes twice then succeeds shouldn't stay one slip from a lockout.
        _throttle.Reset(request.Email);
        return Ok(result);
    }
}
