using RecruitOps.Domain.Common;

namespace RecruitOps.Domain.Entities;

/// <summary>One try at an emailed code (ADR-0030 §5a). Written <b>before</b> the code is compared, then
/// counted: a try is only compared if it is among the first <see cref="OfferPortal.MaxCodeAttempts"/>
/// committed for its code. Inserts never conflict, so parallel guesses are each counted — which a
/// shared counter read-then-written by every request does not guarantee.</summary>
public class OfferPortalAttempt : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid OfferPortalAccessId { get; set; }
}
