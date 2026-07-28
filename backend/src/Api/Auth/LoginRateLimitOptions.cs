namespace RecruitOps.Api.Auth;

/// <summary>Bound from <c>RateLimit:Login</c>.
///
/// <para>These are read through <c>IOptions&lt;T&gt;</c> from inside the partitioner rather
/// than into locals at startup. Configuration sources added <em>after</em> the top-level
/// statements run — which is exactly what <c>WebApplicationFactory</c> does in the test
/// host — would otherwise be ignored, and the limiter would silently keep the production
/// defaults while the tests believed they had raised them.</para>
/// </summary>
public class LoginRateLimitOptions
{
    /// <summary>Requests per window per client address.
    ///
    /// <para>Sized for an office behind one NAT address, and — until a deployment sets
    /// <c>ReverseProxy:TrustForwardedHeaders</c> — for a whole company arriving through one
    /// nginx container. The fixed window counts successful logins too, so a value tuned for
    /// a hostile single host would lock a customer out at 09:00 on Monday. Brute force is
    /// blocked by the per-account throttle, not by this number.</para></summary>
    public int PermitLimit { get; set; } = 60;

    public int WindowSeconds { get; set; } = 60;
}

/// <summary>Bound from <c>RateLimit:PublicApply</c>. Read lazily for the same reason as
/// <see cref="LoginRateLimitOptions"/>.</summary>
public class PublicApplyRateLimitOptions
{
    /// <summary>Higher than the login limit on purpose. A job link shared to Facebook
    /// produces a genuine burst of page views from one office or campus NAT address, and
    /// throttling that turns a successful post into a broken careers page.</summary>
    public int PermitLimit { get; set; } = 120;

    public int WindowSeconds { get; set; } = 60;
}
