using System.ComponentModel.DataAnnotations;

namespace RecruitOps.Application.DTOs;

/// <summary>A panel member on an interview.</summary>
/// <param name="HasSubmittedScorecard">Whether this person has finished their evaluation.
/// Deliberately visible to the whole panel: knowing a colleague is done reveals nothing
/// about what they said, and it is what lets a lead chase the outstanding scorecard.</param>
public record InterviewParticipantDto(
    Guid UserId,
    string DisplayName,
    string? Email,
    bool IsLead,
    bool HasSubmittedScorecard);

/// <summary>One row of the interviews list (<c>GET /api/interviews</c>).
///
/// <para><b>What this record deliberately does not carry: any evaluation.</b> No rating, no
/// recommendation, no summary comment. <see cref="SubmittedCount"/> says how many of the panel
/// have finished — which is public to the panel by design, the same rule as
/// <see cref="InterviewParticipantDto.HasSubmittedScorecard"/> — and that is the closest this
/// list comes to a score. Reading an evaluation goes through
/// <c>GET /interviews/{id}/scorecards</c>, where the blind rule (ADR-0017 §3) is applied. A
/// "recommendation" column added here would route around it.</para></summary>
/// <param name="MyScorecardOutstanding">True when the caller is on the panel and has not
/// submitted. The one thing on this screen that is genuinely actionable, and nothing else in
/// the product surfaces it.</param>
public record InterviewListItemDto(
    Guid Id,
    Guid JobApplicationId,
    string CandidateName,
    string JobPostingTitle,
    Guid DepartmentId,
    string DepartmentName,
    int Round,
    DateTimeOffset ScheduledStart,
    int DurationMinutes,
    string Mode,
    string? Location,
    string Status,
    IReadOnlyList<string> PanelNames,
    int PanelSize,
    int SubmittedCount,
    bool IsOnPanel,
    bool MyScorecardOutstanding);

/// <summary>One scheduled round.</summary>
public record InterviewDto(
    Guid Id,
    Guid JobApplicationId,
    int Round,
    DateTimeOffset ScheduledStart,
    int DurationMinutes,
    string Mode,
    string? Location,
    string Status,
    string? Agenda,
    string? CancellationReason,
    Guid? ScorecardTemplateId,
    string? ScorecardTemplateName,
    IReadOnlyList<InterviewParticipantDto> Participants);

public record ScheduleInterviewRequest
{
    [Required]
    public DateTimeOffset ScheduledStart { get; init; }

    [Range(5, 480)]
    public int DurationMinutes { get; init; } = 60;

    /// <summary>One of the <c>InterviewMode</c> names.</summary>
    [Required, StringLength(20)]
    public string Mode { get; init; } = "OnSite";

    [StringLength(500)]
    public string? Location { get; init; }

    [StringLength(4000)]
    public string? Agenda { get; init; }

    /// <summary>The panel. Must not be empty — an interview with nobody assigned cannot be
    /// scored, and would sit in the pipeline looking scheduled.</summary>
    [Required, MinLength(1)]
    public IReadOnlyList<Guid> ParticipantUserIds { get; init; } = Array.Empty<Guid>();

    /// <summary>Optional lead; must appear in <see cref="ParticipantUserIds"/>.</summary>
    public Guid? LeadUserId { get; init; }
}

/// <summary>Rescheduling. Same shape as scheduling minus the panel, which is changed
/// separately — moving the time and swapping an interviewer are different intentions, and
/// a single endpoint would silently wipe a panel whenever a caller omitted it.</summary>
public record RescheduleInterviewRequest
{
    [Required]
    public DateTimeOffset ScheduledStart { get; init; }

    [Range(5, 480)]
    public int DurationMinutes { get; init; } = 60;

    [Required, StringLength(20)]
    public string Mode { get; init; } = "OnSite";

    [StringLength(500)]
    public string? Location { get; init; }

    [StringLength(4000)]
    public string? Agenda { get; init; }
}

public record SetPanelRequest
{
    [Required, MinLength(1)]
    public IReadOnlyList<Guid> ParticipantUserIds { get; init; } = Array.Empty<Guid>();

    public Guid? LeadUserId { get; init; }
}

public record CancelInterviewRequest
{
    [StringLength(1000)]
    public string? Reason { get; init; }
}

public record CompleteInterviewRequest
{
    /// <summary>True when the candidate did not attend — recorded as <c>NoShow</c> rather
    /// than <c>Completed</c>, because Module 5 will want to tell those apart.</summary>
    public bool NoShow { get; init; }
}
