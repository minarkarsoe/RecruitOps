using RecruitOps.Domain.Common;
using RecruitOps.Domain.Enums;

namespace RecruitOps.Domain.Entities;

/// <summary>A hiring company (CRM record) belonging to an agency (Module 2).</summary>
public class Client : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public ClientTier Tier { get; set; } = ClientTier.Bronze;
    // TODO: CompanyName, ContactPerson, Email, Phone, Industry, Notes ...
}
