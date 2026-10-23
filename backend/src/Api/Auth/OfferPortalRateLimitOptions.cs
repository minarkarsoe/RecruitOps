namespace RecruitOps.Api.Auth;

/// <summary>Per-address limit on the candidate's offer link (<see cref="RateLimitPolicies.OfferPortal"/>).
/// A candidate opens it a handful of times; nobody legitimate needs more than this.</summary>
public class OfferPortalRateLimitOptions
{
    public int PermitLimit { get; set; } = 30;

    public int WindowSeconds { get; set; } = 60;
}
