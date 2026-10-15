namespace RecruitOps.Api.Auth;

/// <summary><c>RateLimit:Refresh</c> — per-IP limit on <c>/api/auth/refresh</c> and
/// <c>/api/auth/revoke</c> (ADR-0028). A signed-in browser refreshes about once per 8-hour access
/// token, so a whole office behind one address stays far below this; something hammering the
/// endpoint with guesses or stolen tokens does not.</summary>
public class RefreshRateLimitOptions
{
    public int PermitLimit { get; set; } = 60;

    public int WindowSeconds { get; set; } = 60;
}
