using RecruitOps.Domain.Common;
using RecruitOps.Domain.Enums;

namespace RecruitOps.Domain.Entities;

/// <summary>An offer of employment for one application (Module 4.2, ADR-0030).
/// <para>Candidate, position and department are not copied here: they are read through the
/// application, its posting and its requisition, so the offer cannot disagree with them. What
/// is the offer's own: the terms, the letter text, and where it is in its life.</para></summary>
public class Offer : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid JobApplicationId { get; set; }

    /// <summary>With <see cref="Number"/>, the human reference <c>OFR-2026-0042</c>: numbered per
    /// company per calendar year, unique by index.</summary>
    public int Year { get; set; }

    public int Number { get; set; }

    public OfferStatus Status { get; set; } = OfferStatus.Draft;

    /// <summary>MMK per month.</summary>
    public decimal BaseSalary { get; set; }

    /// <summary>MMK per month. Base plus allowances is the package the budget check compares with
    /// the requisition's salary budget (ADR-0030 §2).</summary>
    public decimal Allowances { get; set; }

    public DateOnly JoiningDate { get; set; }

    public int ProbationMonths { get; set; }

    /// <summary>The last day the candidate can answer; the link stops working after it.</summary>
    public DateOnly ExpiresOn { get; set; }

    /// <summary>The template the letter was started from, if any (null = the built-in default).</summary>
    public Guid? OfferTemplateId { get; set; }

    /// <summary>This offer's letter, with merge fields (<c>{{BaseSalary}}</c>…) rendered from the
    /// current terms at preview and at send. Copied from the template, editable for this offer only.</summary>
    public string LetterBody { get; set; } = string.Empty;

    public Guid CreatedByUserId { get; set; }

    public DateTimeOffset? SubmittedAt { get; set; }

    public DateTimeOffset? ApprovedAt { get; set; }

    public DateTimeOffset? SentAt { get; set; }
}
