using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecruitOps.Api.Auth;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;

namespace RecruitOps.Api.Controllers;

/// <summary>The delivery log — the read side of ADR-0026's outbox.
///
/// <para><b>Read-only, and it stays that way.</b> There is no "retry" endpoint here on purpose:
/// the worker already retries with backoff up to the attempt cap, and a button that re-queues a
/// row a human is looking at would race the worker for the same row. A row that has genuinely
/// given up needs the underlying problem fixed — a corrected email address, a working relay —
/// not another attempt at the same bad send.</para>
///
/// <para><b><see cref="Policies.InternalUser"/>, not <see cref="Policies.RecruitmentStaff"/>.</b>
/// A Hiring Manager has a real reason to ask whether their candidate was told, and the service
/// restricts them to their own departments (ADR-0003). Note what this policy already excludes:
/// <c>Interviewer</c> is not in it, so a panel member never reaches the log — their legitimate
/// reach is one application they are sitting on, not the company's outbox. <c>Approver</c> is in
/// the policy and is turned away by the service instead, because ADR-0018 makes that a
/// candidate-data decision rather than a routing one.</para>
///
/// <para>⚠️ As with every <see cref="Policies.InternalUser"/> endpoint: <b>this attribute is not
/// the access control.</b> It establishes that somebody is logged in and internal. The filter
/// that matters is applied explicitly in <see cref="IDeliveryLogService"/>.</para>
/// </summary>
[ApiController]
[Route("api/delivery")]
[Authorize(Policy = Policies.InternalUser)]
public class DeliveryController : ControllerBase
{
    private readonly IDeliveryLogService _delivery;

    public DeliveryController(IDeliveryLogService delivery)
    {
        _delivery = delivery;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<DeliveryLogEntryDto>>> Get(
        [FromQuery] DeliveryLogQuery query, CancellationToken ct)
    {
        var result = await _delivery.QueryAsync(query, ct);
        return Ok(result);
    }
}
