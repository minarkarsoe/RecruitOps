using RecruitOps.Domain.Common;
using RecruitOps.Domain.Enums;

namespace RecruitOps.Domain.Entities;

/// <summary>A request to open a headcount, raised by a Hiring Manager (Module 1.1).
/// An approved requisition is what a JobPosting is created from.</summary>
public class Requisition : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    /// <summary>Owning department — the axis Hiring Managers are scoped to (ADR-0003).</summary>
    public Guid DepartmentId { get; set; }

    /// <summary>The user who raised it.</summary>
    public Guid RequestedByUserId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string JobDescription { get; set; } = string.Empty;

    public int Headcount { get; set; } = 1;

    /// <summary>Monthly salary budget per head, in the company's currency.</summary>
    public decimal? SalaryBudget { get; set; }

    public RequisitionStatus Status { get; set; } = RequisitionStatus.Draft;

    public DateTimeOffset? SubmittedAt { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
}
