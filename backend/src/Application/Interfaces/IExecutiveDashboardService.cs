using RecruitOps.Application.DTOs;
using RecruitOps.Domain;

namespace RecruitOps.Application.Interfaces;

/// <summary>Module 5.1's executive dashboard (ADR-0031). Every number is computed over the rows the caller
/// may see — never a company figure filtered for display. Null for a caller who sees nothing (an approver,
/// an interviewer) or names a department out of their reach.</summary>
public interface IExecutiveDashboardService
{
    Task<ExecutiveDashboardDto?> GetAsync(ReportingPeriod.Kind period, Guid? departmentId, Guid? recruiterId, CancellationToken ct = default);
}
