using RecruitOps.Domain.Common;

namespace RecruitOps.Domain.Entities;

/// <summary>Two candidates a person looked at and said are <b>not</b> the same (Module 2.7) — so the
/// pair stops being flagged as a possible duplicate.
///
/// <para>It exists because "not the same person" is the frequent answer here, not the edge case:
/// common Burmese names collide, and a family shares a phone. Without a record of it, the same pair
/// would be flagged forever and the reviewer would eventually merge two strangers to clear it.</para>
///
/// <para>Stored once per pair, smaller id first (<see cref="Guid.CompareTo(Guid)"/>), so the pair
/// has one spelling and the unique index can hold.</para></summary>
public class DistinctCandidatePair : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid FirstCandidateId { get; set; }

    public Guid SecondCandidateId { get; set; }

    /// <summary>Who decided — the record a reviewer would want if the call turns out wrong.</summary>
    public Guid DecidedByUserId { get; set; }
}
