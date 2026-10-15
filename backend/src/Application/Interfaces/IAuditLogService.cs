using RecruitOps.Application.DTOs;

namespace RecruitOps.Application.Interfaces;

/// <summary>Reads the audit log (ADR-0027). The write side is <see cref="IAuditTrail"/>.</summary>
public interface IAuditLogService
{
    Task<PagedResult<AuditLogEntryDto>> QueryAsync(AuditLogQuery query, CancellationToken ct = default);
}
