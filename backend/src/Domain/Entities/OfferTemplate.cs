using RecruitOps.Domain.Common;

namespace RecruitOps.Domain.Entities;

/// <summary>An offer-letter template with merge fields (Module 4.2). An offer copies the body when it
/// is created, so editing a template never rewrites a letter already drafted or sent.</summary>
public class OfferTemplate : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
