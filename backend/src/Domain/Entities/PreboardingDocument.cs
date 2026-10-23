using RecruitOps.Domain.Common;
using RecruitOps.Domain.Enums;

namespace RecruitOps.Domain.Entities;

/// <summary>One joining document of an accepted offer — its state, its review and, for
/// <see cref="PreboardingDocumentKind.BankAccount"/>, the typed details (ADR-0030 §5b). Created by the
/// candidate's first change to it; one per offer and kind, by index.
/// <para><b><see cref="Revision"/> is a concurrency token, and every change bumps it</b> — an upload, a
/// removal, saving bank details, an approval, a return. A recruiter's decision names the revision they
/// were shown, so nobody approves a file they did not see, and two uploads at once cannot both
/// commit.</para></summary>
public class PreboardingDocument : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid OfferId { get; set; }

    public PreboardingDocumentKind Kind { get; set; }

    public PreboardingDocumentStatus Status { get; set; } = PreboardingDocumentStatus.Missing;

    public int Revision { get; set; }

    /// <summary>The candidate's last change.</summary>
    public DateTimeOffset? ChangedAt { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    public Guid? ReviewedByUserId { get; set; }

    /// <summary>What the recruiter wrote when sending it back — to the candidate, in the recruiter's
    /// words. Kept after the candidate changes the document; shown to them only while it is returned.</summary>
    public string? ReturnMessage { get; set; }

    public string? BankName { get; set; }

    public string? BankAccountName { get; set; }

    /// <summary>ASCII digits only. ⚠️ Masked to its last four digits in every response; the one way to
    /// read it whole is the recruiter's audited Reveal.</summary>
    public string? BankAccountNumber { get; set; }
}
