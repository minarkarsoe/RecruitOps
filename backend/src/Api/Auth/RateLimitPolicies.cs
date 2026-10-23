namespace RecruitOps.Api.Auth;

/// <summary>Named rate-limiting policies.</summary>
public static class RateLimitPolicies
{
    /// <summary>Per-IP limit on credential submission. Pairs with <c>ILoginThrottle</c>,
    /// which limits per account — one IP guessing many accounts and many IPs guessing one
    /// account are different attacks, and each control only sees one of them.</summary>
    public const string Login = "login";

    /// <summary>The public job page and application form. Anonymous <em>and</em> writing, so
    /// without a limit anyone can fill a customer's candidate database with junk. Separate
    /// from <see cref="Login"/> because the two have nothing in common but their anonymity:
    /// a shared job link legitimately gets a burst of views, and failed logins never do.</summary>
    public const string PublicApply = "public-apply";

    /// <summary><c>/api/auth/refresh</c> and <c>/api/auth/revoke</c>: anonymous, and each call is a
    /// guess at a session if the token is not the caller's (ADR-0028).</summary>
    public const string Refresh = "refresh";

    /// <summary>The candidate's offer link (ADR-0030 §5a). Anonymous, and the code step is a guess
    /// at a credential; the per-offer limits on codes and tries are the service's, this is the
    /// per-address one in front of them.</summary>
    public const string OfferPortal = "offer-portal";
}
