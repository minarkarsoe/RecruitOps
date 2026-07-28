using System.ComponentModel.DataAnnotations;

namespace RecruitOps.Application.DTOs;

// ---------- Templates (3.3 configuration) ----------

public record ScorecardCriterionDto(
    Guid Id,
    int Sequence,
    string Label,
    string? Guidance,
    string Type,
    bool IsRequired);

public record ScorecardTemplateDto(
    Guid Id,
    string Name,
    string? Description,
    Guid? DepartmentId,
    string? DepartmentName,
    Guid? JobPostingId,
    bool IsActive,
    IReadOnlyList<ScorecardCriterionDto> Criteria);

public record ScorecardCriterionInput
{
    [Required, StringLength(200)]
    public string Label { get; init; } = string.Empty;

    [StringLength(1000)]
    public string? Guidance { get; init; }

    /// <summary>One of the <c>CriterionType</c> names.</summary>
    [Required, StringLength(20)]
    public string Type { get; init; } = "Rating";

    public bool IsRequired { get; init; } = true;
}

public record SaveScorecardTemplateRequest
{
    [Required, StringLength(200)]
    public string Name { get; init; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; init; }

    /// <summary>Department-level template. Mutually exclusive with
    /// <see cref="JobPostingId"/>; both null makes this the company-wide default.</summary>
    public Guid? DepartmentId { get; init; }

    /// <summary>Override for a single posting. Mutually exclusive with
    /// <see cref="DepartmentId"/>.</summary>
    public Guid? JobPostingId { get; init; }

    public bool IsActive { get; init; } = true;

    /// <summary>Criteria in display order. <c>Sequence</c> is derived from this list's order,
    /// so gaps and duplicates cannot be expressed — same approach as <c>ApprovalChainStep</c>.</summary>
    [Required, MinLength(1)]
    public IReadOnlyList<ScorecardCriterionInput> Criteria { get; init; } =
        Array.Empty<ScorecardCriterionInput>();
}

// ---------- Filling one in ----------

public record ScorecardResponseDto(
    Guid ScorecardCriterionId,
    string CriterionLabel,
    string CriterionType,
    int? Rating,
    bool? YesNo,
    string? Comment);

/// <summary>One interviewer's evaluation.</summary>
public record ScorecardDto(
    Guid Id,
    Guid InterviewId,
    Guid InterviewerUserId,
    string InterviewerName,
    string Status,
    DateTimeOffset? SubmittedAt,
    string? Recommendation,
    string? SummaryComment,
    IReadOnlyList<ScorecardResponseDto> Responses);

/// <summary>The caller's own scorecard plus the criteria they are being asked to fill in.
/// <para>Criteria travel with it rather than being fetched separately so the form cannot be
/// rendered against a template the interview isn't actually scored on.</para></summary>
public record MyScorecardDto(
    Guid InterviewId,
    Guid? ScorecardTemplateId,
    string? ScorecardTemplateName,
    IReadOnlyList<ScorecardCriterionDto> Criteria,
    ScorecardDto? Scorecard);

/// <summary>What the panel view returns.</summary>
/// <param name="Visible">The scorecards the caller may read right now.</param>
/// <param name="HiddenCount">How many submitted scorecards are being withheld. Shown as a
/// count, not a list: "2 evaluations are waiting for yours" is the nudge that makes the
/// blind rule feel like a process rather than a bug, and a bare count reveals nothing about
/// their content.</param>
/// <param name="BlindedUntilYouSubmit">True when the caller is a panel member who has not
/// submitted, i.e. the reason anything is hidden (ADR-0017 §3).</param>
public record InterviewScorecardsDto(
    Guid InterviewId,
    IReadOnlyList<ScorecardDto> Visible,
    int HiddenCount,
    bool BlindedUntilYouSubmit);

public record ScorecardAnswerInput
{
    [Required]
    public Guid ScorecardCriterionId { get; init; }

    [Range(1, 5)]
    public int? Rating { get; init; }

    public bool? YesNo { get; init; }

    [StringLength(4000)]
    public string? Comment { get; init; }
}

public record SaveScorecardRequest
{
    /// <summary>One of the <c>HireRecommendation</c> names. Required to submit, optional
    /// while still a draft.</summary>
    [StringLength(20)]
    public string? Recommendation { get; init; }

    [StringLength(4000)]
    public string? SummaryComment { get; init; }

    public IReadOnlyList<ScorecardAnswerInput> Answers { get; init; } =
        Array.Empty<ScorecardAnswerInput>();
}
