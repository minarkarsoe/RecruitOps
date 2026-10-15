using RecruitOps.Application.DTOs;

namespace RecruitOps.Application.Interfaces;

/// <summary>Reads the outbox for humans (ADR-0026). Write-side is
/// <see cref="Domain.Entities.OutboundMessage"/> and the workers; nothing here sends anything.
///
/// <para>⚠️ <b>Department scoping is applied here, explicitly, and it is the security-critical
/// filter (ADR-0003).</b> The tenant filter on <c>OutboundMessages</c> is a global EF filter and
/// looks after itself; department reach does not, and this table has no department of its own —
/// it reaches one only through <see cref="Domain.Entities.OutboundMessage.SubjectType"/> /
/// <c>SubjectId</c>. That indirection is exactly the kind of thing that gets forgotten, so the
/// implementation resolves it per subject type and <b>hides rows it cannot resolve</b> from a
/// scoped user rather than showing them.</para>
/// </summary>
public interface IDeliveryLogService
{
    Task<PagedResult<DeliveryLogEntryDto>> QueryAsync(
        DeliveryLogQuery query, CancellationToken ct = default);
}
