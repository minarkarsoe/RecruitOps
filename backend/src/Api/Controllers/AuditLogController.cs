using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecruitOps.Api.Authorization;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;

namespace RecruitOps.Api.Controllers;

/// <summary>The audit log (ADR-0027) — who changed access, and who signed in.
/// <para><b>Read-only, and it stays that way.</b> There is no endpoint that edits or deletes an
/// entry, and the database refuses to as well.</para></summary>
[ApiController]
[Route("api/audit-log")]
[Authorize]
public class AuditLogController : ControllerBase
{
    private readonly IAuditLogService _log;

    public AuditLogController(IAuditLogService log) => _log = log;

    [HttpGet]
    [HasPermission("permission:system:system:audit")]
    public async Task<ActionResult<PagedResult<AuditLogEntryDto>>> Get([FromQuery] AuditLogQuery query, CancellationToken ct) =>
        Ok(await _log.QueryAsync(query, ct));
}
