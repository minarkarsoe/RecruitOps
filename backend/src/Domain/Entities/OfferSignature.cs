using RecruitOps.Domain.Common;

namespace RecruitOps.Domain.Entities;

/// <summary>The evidence of a candidate accepting an offer (ADR-0030 §5, §5a): a typed full name and a
/// consent, with what is needed to show later exactly what was agreed and by whom.
/// <para>One per offer, by unique index. Written once and never changed.</para></summary>
public class OfferSignature : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid OfferId { get; set; }

    /// <summary>The name as the candidate typed it (on one line) — the signature itself.</summary>
    public string TypedName { get; set; } = string.Empty;

    public DateTimeOffset SignedAt { get; set; }

    /// <summary>The client address, after the edge's real-IP handling (ADR-0029 L6).</summary>
    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    /// <summary>Where the code that unlocked the offer was sent — the mailbox the signer had to read.</summary>
    public string? SentToEmail { get; set; }

    /// <summary>The letter exactly as shown to the candidate when they signed. Kept, not re-rendered:
    /// a later change to a name or the company's would otherwise change what they appear to have signed.</summary>
    public string LetterText { get; set; } = string.Empty;

    /// <summary>SHA-256 of <see cref="LetterText"/> (UTF-8), lowercase hex.</summary>
    public string LetterSha256 { get; set; } = string.Empty;
}
