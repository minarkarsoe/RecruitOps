using System.ComponentModel.DataAnnotations;

namespace RecruitOps.Application.DTOs;

/// <summary>One row of the Offer Dashboard (Module 4.1, <c>design/internal/offer-dashboard.html</c>).</summary>
/// <param name="Reference"><c>OFR-2026-0042</c>.</param>
/// <param name="BaseSalary">MMK per month; null when the viewer may not see salaries (a Hiring
/// Manager, unless the company shows them — ADR-0030 §4).</param>
/// <param name="AwaitingMe">The viewer holds a waiting step of the live approval round.</param>
/// <param name="WaitingOn">While pending approval, the label of the step whose turn it is.</param>
/// <param name="ViewedAt">When the candidate unlocked it — the design's "Opened 17 Aug · not signed".</param>
/// <param name="RespondedAt">When the candidate answered — "Signed 13 Aug · joining 1 Oct".</param>
public record OfferListItemDto(
    Guid Id,
    string Reference,
    Guid JobApplicationId,
    string CandidateName,
    string Position,
    Guid DepartmentId,
    string DepartmentName,
    decimal? BaseSalary,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SentAt,
    bool AwaitingMe,
    string? WaitingOn,
    DateTimeOffset? ViewedAt,
    DateTimeOffset? RespondedAt,
    DateOnly JoiningDate,
    DateTimeOffset? LastRemindedAt);

/// <summary>One offer (Module 4.2, <c>design/internal/offer-create.html</c>).
/// <para>Every money field — and the letter, which quotes them — is null when
/// <see cref="SalaryVisible"/> is false: the API omits them rather than trusting a screen to hide
/// them.</para></summary>
/// <param name="SalaryBudget">The requisition's budget, MMK per month per hire.</param>
/// <param name="OverBudget">Base plus allowances exceeds the budget.</param>
/// <param name="NeedsApproval">Over budget, or the company requires approval for every offer
/// (ADR-0030 §2).</param>
/// <param name="PolicyRequiresApproval">The company requires approval for every offer — so the editor
/// can tell, while terms are being typed, whether bringing the package within budget is enough.</param>
/// <param name="CanManage">The viewer may edit, submit, revise and delete (recruitment staff). A
/// display hint; the API re-checks every action.</param>
/// <param name="ApprovalPreview">For a Draft, seen by recruitment staff: the step labels the offer
/// chain would use if submitted now ("Approval this will need") — sent even while the saved terms
/// need no approval, because the editor decides that from the terms being typed. Null when not
/// applicable — or when no offer chain is configured, which the editor then says.</param>
/// <param name="ViewedAt">When the candidate first unlocked the offer with a code (ADR-0030 §5a).</param>
/// <param name="RespondedAt">When the candidate accepted or declined.</param>
/// <param name="DeclineReason">The candidate's own words, if they gave any. Follows the salary rule —
/// "the salary is too low" is the package in other words.</param>
/// <param name="Signature">The acceptance evidence — recruitment staff only.</param>
/// <param name="LastRemindedAt">When the candidate was last reminded; one reminder per 24 hours.</param>
/// <param name="CandidateEmail">Where the offer goes — or, once sent, went and still goes: every code and
/// reminder uses the address fixed at sending. Recruitment staff only, so they can check it before sending.</param>
public record OfferDetailDto(
    Guid Id,
    string Reference,
    string Status,
    Guid JobApplicationId,
    Guid RequisitionId,
    string CandidateName,
    string Position,
    Guid DepartmentId,
    string DepartmentName,
    bool SalaryVisible,
    decimal? BaseSalary,
    decimal? Allowances,
    decimal? SalaryBudget,
    bool? OverBudget,
    bool? NeedsApproval,
    bool? PolicyRequiresApproval,
    DateOnly JoiningDate,
    int ProbationMonths,
    DateOnly ExpiresOn,
    Guid? OfferTemplateId,
    string? LetterBody,
    bool CanManage,
    IReadOnlyList<string>? ApprovalPreview,
    IReadOnlyList<ApprovalStepDto> Approvals,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? ApprovedAt,
    DateTimeOffset? SentAt,
    DateTimeOffset? ViewedAt,
    DateTimeOffset? RespondedAt,
    string? DeclineReason,
    OfferSignatureDto? Signature,
    DateTimeOffset? LastRemindedAt,
    string? CandidateEmail);

/// <summary>Sending an offer. <see cref="ExpectedEmail"/> is the address the recruiter was shown and
/// confirmed; if the candidate's address has changed since, sending is refused rather than going somewhere
/// they did not check (security re-review, 2026-10-23).</summary>
public record SendOfferRequest
{
    [StringLength(320)]
    public string? ExpectedEmail { get; init; }
}

/// <summary>What was recorded when the candidate accepted (ADR-0030 §5a).</summary>
public record OfferSignatureDto(
    string TypedName,
    DateTimeOffset SignedAt,
    string? IpAddress,
    string? UserAgent,
    string LetterSha256,
    string? SentToEmail);

/// <summary>An application at the Offer stage with no open offer — what "Create offer" can start from.</summary>
public record OfferEligibleApplicationDto(
    Guid JobApplicationId,
    string CandidateName,
    string Position,
    string DepartmentName,
    DateTimeOffset AppliedAt);

public record CreateOfferRequest
{
    [Required]
    public Guid JobApplicationId { get; init; }

    /// <summary>The template to start the letter from; null = the built-in default.</summary>
    public Guid? OfferTemplateId { get; init; }
}

/// <summary>A Draft's terms and letter. Only a Draft accepts this (ADR-0030 §3).</summary>
public record UpdateOfferRequest
{
    /// <summary>MMK per month.</summary>
    [Range(typeof(decimal), "0", "1000000000000")]
    public decimal BaseSalary { get; init; }

    /// <summary>MMK per month.</summary>
    [Range(typeof(decimal), "0", "1000000000000")]
    public decimal Allowances { get; init; }

    /// <summary>Nullable so that <c>[Required]</c> means something: a missing date is a 400, not 0001-01-01.</summary>
    [Required]
    public DateOnly? JoiningDate { get; init; }

    [Range(0, 24)]
    public int ProbationMonths { get; init; }

    [Required]
    public DateOnly? ExpiresOn { get; init; }

    [Required, StringLength(20000, MinimumLength = 1)]
    public string LetterBody { get; init; } = string.Empty;
}

/// <summary>The letter as the candidate would read it: merge fields filled from the current terms.</summary>
public record OfferLetterDto(string Text);

public record OfferTemplateDto(Guid? Id, string Name, string Body, bool IsActive);

public record SaveOfferTemplateRequest
{
    [Required, StringLength(200, MinimumLength = 2)]
    public string Name { get; init; } = string.Empty;

    [Required, StringLength(20000, MinimumLength = 1)]
    public string Body { get; init; } = string.Empty;

    public bool IsActive { get; init; } = true;
}

/// <summary>The company's offer policy (ADR-0030 §2, §4).</summary>
public record OfferSettingsDto(bool RequireOfferApproval, bool ShowOfferSalaryToHiringManagers);

/// <summary>Both values, always: nullable so an omitted field is a 400 rather than a silent
/// <c>false</c> that switches a financial control off.</summary>
public record UpdateOfferSettingsRequest
{
    [Required]
    public bool? RequireOfferApproval { get; init; }

    [Required]
    public bool? ShowOfferSalaryToHiringManagers { get; init; }
}
