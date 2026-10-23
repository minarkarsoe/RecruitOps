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

    /// <summary>The candidate's link: <c>/offer/{PortalToken}</c> on the public site. Set when the offer
    /// is sent. <b>An address, not a credential</b> (ADR-0030 §5a) — it shows nothing sensitive on its
    /// own, and is stored as is so a reminder can repeat it. The emailed code is the credential.</summary>
    public string? PortalToken { get; set; }

    /// <summary>The address the offer was sent to, fixed at sending. Every later email — reminders, codes,
    /// the signed copy — goes here and nowhere else: read live, a candidate record whose email was filled
    /// in afterwards by a stranger's application could redirect the codes (security review, 2026-10-23).</summary>
    public string? SentToEmail { get; set; }

    /// <summary>When the candidate first unlocked the offer with a code — not when the link was opened,
    /// which shows nothing to view.</summary>
    public DateTimeOffset? ViewedAt { get; set; }

    /// <summary>When the candidate accepted or declined.</summary>
    public DateTimeOffset? RespondedAt { get; set; }

    /// <summary>The candidate's own words when declining, if any.</summary>
    public string? DeclineReason { get; set; }

    public DateTimeOffset? LastRemindedAt { get; set; }

    public int ReminderCount { get; set; }
}
