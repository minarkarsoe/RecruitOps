using RecruitOps.Application.DTOs;

namespace RecruitOps.Application.Interfaces;

/// <summary>Module 2.7 — two candidates created separately that may be one person. The system flags,
/// a person decides (<c>design/internal/talent-pool.html</c>, "the merge nobody should do blind").</summary>
public interface ICandidateDuplicateService
{
    /// <summary>Pairs of unmerged candidates sharing a normalised phone or email, minus pairs marked
    /// "not the same person" and values on more than ten records (a shared line). Newest pairs first,
    /// capped.</summary>
    Task<IReadOnlyList<DuplicatePairDto>> ListPairsAsync(CancellationToken ct = default);

    /// <summary>Folds one record into the other. Null when either does not exist.
    /// <see cref="InvalidOperationException"/> when the two share no phone or email, either is already
    /// merged, they were marked different people, or another decision about either record got there
    /// first.</summary>
    Task<MergeCandidatesResultDto?> MergeAsync(MergeCandidatesRequest request, CancellationToken ct = default);

    /// <summary>Remembers that two records are different people; idempotent. Null when either does not
    /// exist; <see cref="InvalidOperationException"/> as for a merge.</summary>
    Task<bool?> KeepSeparateAsync(KeepCandidatesSeparateRequest request, CancellationToken ct = default);
}
