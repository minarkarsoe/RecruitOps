using RecruitOps.Domain.Common;

namespace RecruitOps.Domain.Entities;

/// <summary>Token for a public, no-login job page that candidates open to view a vacancy
/// and apply (Module 2.1). ⚠️ Repurposed: in the agency model this was a client CV-review
/// portal — that meaning is gone (ADR-0001).
///
/// <para>The token is the <b>only</b> credential on that page, so it is generated from a
/// cryptographic RNG and is long enough not to be guessable or enumerable. It is also the
/// only way an anonymous request can identify a tenant, since there is no JWT to read a
/// <c>tenant_id</c> claim from.</para>
/// </summary>
public class PortalLink : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid JobPostingId { get; set; }

    /// <summary>URL-safe, unguessable. Unique across the database.</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>Null means it lives as long as the posting is Live. A link shared to
    /// Facebook outlives the vacancy, so expiry is about stale applications, not secrecy.</summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>Kill switch that does not require closing the posting — e.g. when a link
    /// was shared to the wrong audience.</summary>
    public bool IsRevoked { get; set; }

    public int ViewCount { get; set; }
    public int ApplyCount { get; set; }
}
