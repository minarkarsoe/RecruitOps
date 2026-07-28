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
}
