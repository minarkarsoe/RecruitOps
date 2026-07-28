using System.ComponentModel.DataAnnotations;

namespace RecruitOps.Application.DTOs;

public record NoteMentionDto(Guid UserId, string DisplayName);

/// <summary>A comment on an application (3.4).</summary>
/// <param name="Body">Exactly what the author typed, unescaped. Safe in JSON; a client that
/// renders it must escape it itself.</param>
/// <param name="BodyHtml">The same text escaped, with resolved mentions marked up. This is
/// what the SPA renders — it exists so that "escape on output" is the default path rather
/// than something each caller has to remember.</param>
public record NoteDto(
    Guid Id,
    Guid JobApplicationId,
    Guid? InterviewId,
    Guid AuthorUserId,
    string AuthorName,
    string Body,
    string BodyHtml,
    DateTimeOffset CreatedAt,
    IReadOnlyList<NoteMentionDto> Mentions);

public record CreateNoteRequest
{
    [Required, StringLength(4000, MinimumLength = 1)]
    public string Body { get; init; } = string.Empty;

    /// <summary>Optionally pin the note to one interview round.</summary>
    public Guid? InterviewId { get; init; }
}
