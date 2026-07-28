using RecruitOps.Domain.Common;
using RecruitOps.Domain.Enums;

namespace RecruitOps.Domain.Entities;

/// <summary>A person in the talent pool.
///
/// <para><b>Duplicate detection (Module 2.7) is a lookup, not a constraint.</b> Two people
/// can share a household phone number, and one person can apply twice with different
/// addresses — so <see cref="Email"/> and <see cref="Phone"/> are indexed for matching but
/// never made unique. A hard constraint here would reject real applicants.</para>
/// </summary>
public class Candidate : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public string FullName { get; set; } = string.Empty;

    /// <summary>Stored lower-cased so matching doesn't depend on database collation.</summary>
    public string? Email { get; set; }

    /// <summary>Stored digits-only (see <c>PhoneNormalizer</c>) so "+95 9 123 456" and
    /// "09123456" are recognised as the same person. The formatting the candidate typed is
    /// not worth keeping — being able to detect the duplicate is.</summary>
    public string? Phone { get; set; }

    public SourceChannel Source { get; set; } = SourceChannel.Direct;

    /// <summary>Set when this record has been merged into another. Kept rather than deleted
    /// so existing applications and interview history don't lose their subject.</summary>
    public Guid? MergedIntoCandidateId { get; set; }

    // TODO (Module 2.3+): Skills, Experience, CvDocument — arrive with OCR/profiling.
}
