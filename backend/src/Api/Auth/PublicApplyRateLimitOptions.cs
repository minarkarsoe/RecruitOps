namespace RecruitOps.Api.Auth;

/// <summary>Bound from <c>RateLimit:PublicApply</c>. Read lazily for the same reason as
/// <see cref="LoginRateLimitOptions"/>.</summary>
public class PublicApplyRateLimitOptions
{
    /// <summary>Requests per window per client address. Deliberately higher than login: a job
    /// link shared to Facebook produces a genuine burst of views from one office or campus
    /// NAT address, and throttling that turns a successful post into a broken careers page.</summary>
    public int PermitLimit { get; set; } = 120;

    public int WindowSeconds { get; set; } = 60;
}
