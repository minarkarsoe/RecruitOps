using RecruitOps.Domain.Common;
using RecruitOps.Domain.Enums;

namespace RecruitOps.Domain.Entities;

/// <summary>Retainer / SLA contract with a client. Drives expiry notifications (Module 2).</summary>
public class Contract : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid ClientId { get; set; }
    public ContractStatus Status { get; set; } = ContractStatus.Active;
    // TODO: StartDate, EndDate, Value, Terms, LastReminderSentAt ...
}
