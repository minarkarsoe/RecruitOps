using System.ComponentModel.DataAnnotations;

namespace RecruitOps.Application.DTOs;

/// <summary>One row of the Offer Dashboard (Module 4.1, <c>design/internal/offer-dashboard.html</c>).</summary>
/// <param name="Reference"><c>OFR-2026-0042</c>.</param>
/// <param name="BaseSalary">MMK per month; null when the viewer may not see salaries (a Hiring
/// Manager, unless the company shows them — ADR-0030 §4).</param>
/// <param name="AwaitingMe">The viewer holds a waiting step of the live approval round.</param>
/// <param name="WaitingOn">While pending approval, the label of the step whose turn it is.</param>
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
    string? WaitingOn);

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
    DateTimeOffset? SentAt);

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
