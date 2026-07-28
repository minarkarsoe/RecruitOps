using RecruitOps.Application.DTOs;

namespace RecruitOps.Application.Interfaces;

/// <summary>Module 3.4 — collaborative notes with @mentions.</summary>
public interface INoteService
{
    Task<IReadOnlyList<NoteDto>?> ListForApplicationAsync(
        Guid jobApplicationId, CancellationToken ct = default);

    /// <summary>Adds a note. Mentions are parsed from the body server-side and resolved
    /// against users who can actually reach this application — a mention cannot be forged
    /// by the client, and cannot address someone with no business seeing the thread.</summary>
    Task<NoteDto?> CreateAsync(
        Guid jobApplicationId, CreateNoteRequest request, CancellationToken ct = default);
}
