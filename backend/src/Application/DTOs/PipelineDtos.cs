using System.ComponentModel.DataAnnotations;

namespace RecruitOps.Application.DTOs;

/// <summary>A candidate's application, as shown on the pipeline board.</summary>
public record PipelineItemDto(
    Guid Id,
    Guid CandidateId,
    string CandidateName,
    string? Email,
    string? Phone,
    string Status,
    string Source,
    DateTimeOffset AppliedAt,
    string? CoverNote,
    string? CustomFieldsJson);

/// <summary>One recorded stage transition (Module 5 reads these).</summary>
public record StageHistoryItemDto(
    string? FromStatus,
    string ToStatus,
    DateTimeOffset ChangedAt,
    string? ChangedByName,
    string? Note);

public record MoveStageRequest
{
    /// <summary>One of the <c>PipelineStatus</c> names.</summary>
    [Required, StringLength(20)]
    public string ToStatus { get; init; } = string.Empty;

    [StringLength(1000)]
    public string? Note { get; init; }
}
