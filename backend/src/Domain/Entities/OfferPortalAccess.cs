using RecruitOps.Domain.Common;

namespace RecruitOps.Domain.Entities;

/// <summary>One code emailed to a candidate for an offer's link, and — once it was entered correctly —
/// the session it opened (ADR-0030 §5a).
/// <para>Only hashes are stored: the code's (<see cref="CodeHash"/>) and the session cookie's
/// (<see cref="SessionHash"/>). A row is also the record of who asked for a code when, which is what
/// the per-offer daily limit counts. Tries at the code are rows of their own
/// (<see cref="OfferPortalAttempt"/>), not a counter here: a counter read and written by parallel
/// guesses lets all but one of them go uncounted (database review, 2026-10-23).</para></summary>
public class OfferPortalAccess : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid OfferId { get; set; }

    /// <summary>SHA-256 of <c>"{Id}:{code}"</c>, lowercase hex.</summary>
    public string CodeHash { get; set; } = string.Empty;

    public DateTimeOffset CodeExpiresAt { get; set; }

    /// <summary>When the code was used. A concurrency token: a code opens one session, and two correct
    /// entries at once cannot both claim it.</summary>
    public DateTimeOffset? VerifiedAt { get; set; }

    /// <summary>SHA-256 of the session cookie's value, lowercase hex; set when the code is verified.</summary>
    public string? SessionHash { get; set; }

    public DateTimeOffset? SessionExpiresAt { get; set; }
}
