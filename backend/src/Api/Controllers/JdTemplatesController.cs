using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecruitOps.Api.Auth;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;

namespace RecruitOps.Api.Controllers;

/// <summary>Module 1.2 — JD template library.
/// <para>Readable by any internal user (a Hiring Manager drafting a requisition needs
/// them); creation is limited to recruitment staff.</para></summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = Policies.InternalUser)]
public class JdTemplatesController : ControllerBase
{
    private readonly IJdTemplateService _templates;

    public JdTemplatesController(IJdTemplateService templates) => _templates = templates;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<JdTemplateDto>>> Get(CancellationToken ct)
        => Ok(await _templates.GetTemplatesAsync(ct));

    [HttpPost]
    [Authorize(Policy = Policies.RecruitmentStaff)]
    public async Task<ActionResult<JdTemplateDto>> Create(CreateJdTemplateRequest request, CancellationToken ct)
    {
        var created = await _templates.CreateAsync(request, ct);
        return created is null ? NotFound() : Ok(created);
    }
}
