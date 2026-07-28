using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecruitOps.Api.Auth;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;

namespace RecruitOps.Api.Controllers;

/// <summary>Module 3.4 — collaborative notes on an application.
/// <para><see cref="Policies.InternalUser"/> throughout: the whole point is that a hiring
/// manager and a recruiter talk in the same thread. Who may actually see the thread is
/// decided by <c>IApplicationAccess</c> in the service (ADR-0003 / ADR-0017 §4).</para></summary>
[ApiController]
[Route("api/applications/{applicationId:guid}/notes")]
[Authorize(Policy = Policies.InternalUser)]
public class NotesController : ControllerBase
{
    private readonly INoteService _notes;

    public NotesController(INoteService notes) => _notes = notes;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NoteDto>>> List(
        Guid applicationId, CancellationToken ct)
    {
        var result = await _notes.ListForApplicationAsync(applicationId, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<NoteDto>> Create(
        Guid applicationId, CreateNoteRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _notes.CreateAsync(applicationId, request, ct);
            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Title = "Cannot add note", Detail = ex.Message });
        }
    }
}
