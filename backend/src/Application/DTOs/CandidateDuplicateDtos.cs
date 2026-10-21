using System.ComponentModel.DataAnnotations;

namespace RecruitOps.Application.DTOs;

/// <summary>One side of a possible duplicate — what a reviewer needs to tell two records apart
/// (Module 2.7; drawn in <c>design/internal/talent-pool.html</c>).</summary>
/// <param name="Source">The candidate's first source channel, as the enum name.</param>
/// <param name="HasSubmittedScorecard">Any interviewer has submitted an evaluation for one of this
/// record's applications — a reason to keep it rather than fold it into the other.</param>
public record DuplicateCandidateDto(
    Guid Id,
    string FullName,
    string? Email,
    string? Phone,
    string Source,
    DateTimeOffset CreatedAt,
    int ApplicationCount,
    int InterviewCount,
    bool HasSubmittedScorecard);

/// <summary>Two records that may be one person. <see cref="First"/> is the older, and the default to
/// keep.</summary>
/// <param name="MatchedOn">"Phone", "Email", or "Phone and email".</param>
/// <param name="MatchedValue">The shared phone or email (the phone when both match).</param>
public record DuplicatePairDto(
    DuplicateCandidateDto First,
    DuplicateCandidateDto Second,
    string MatchedOn,
    string MatchedValue);

public record MergeCandidatesRequest
{
    /// <summary>The record that stays. Its fields win; its blanks are filled from the other.</summary>
    [Required]
    public Guid KeepCandidateId { get; init; }

    /// <summary>The record folded in. Its applications move; it keeps a pointer to the kept one.</summary>
    [Required]
    public Guid MergeCandidateId { get; init; }
}

public record KeepCandidatesSeparateRequest
{
    [Required]
    public Guid FirstCandidateId { get; init; }

    [Required]
    public Guid SecondCandidateId { get; init; }
}

public record MergeCandidatesResultDto(Guid KeptCandidateId, int ApplicationsMoved);
