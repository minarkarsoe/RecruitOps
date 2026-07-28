using RecruitOps.Domain.Common;
using RecruitOps.Domain.Enums;

namespace RecruitOps.Domain.Entities;

/// <summary>A published vacancy (Module 2.1).
///
/// <para><b>A posting always traces back to an approved Requisition.</b> That link is
/// required, not optional: the entire point of the in-house model is that nobody can
/// advertise a role the business has not approved and budgeted. Allowing a free-standing
/// posting would put a hole straight through Module 1.</para>
/// </summary>
public class JobPosting : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid DepartmentId { get; set; }

    /// <summary>The approved requisition this posting realises. Required — see class remarks.</summary>
    public Guid RequisitionId { get; set; }

    public JobStatus Status { get; set; } = JobStatus.Draft;

    /// <summary>Copied from the requisition at creation, then editable. Recruiters rewrite
    /// an internal JD into candidate-facing copy; forcing them to edit the requisition
    /// instead would alter the document approvers signed off on.</summary>
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public string? Location { get; set; }
    public EmploymentType EmploymentType { get; set; } = EmploymentType.FullTime;

    public int Headcount { get; set; } = 1;

    public decimal? SalaryMin { get; set; }
    public decimal? SalaryMax { get; set; }

    /// <summary>Salary is stored regardless but only shown publicly when this is set.
    /// The requisition's budget is internal information; publishing it by default would
    /// leak the company's pay bands the first time anyone published a job.</summary>
    public bool ShowSalary { get; set; }

    /// <summary>Customer-defined application-form fields, as JSONB (Module 2.2).
    /// Schema-per-customer is the requirement, so this is deliberately not columns.</summary>
    public string? ApplicationFormFieldsJson { get; set; }

    public DateTimeOffset? PostedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
}
