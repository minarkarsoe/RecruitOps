using RecruitOps.Domain.Common;

namespace RecruitOps.Domain.Entities;

/// <summary>A named set of criteria that interviews are scored against (3.3).
///
/// <para><b>Scope is department-level with a per-posting override</b> (ADR-0017 §1). At most
/// one of <see cref="DepartmentId"/> and <see cref="JobPostingId"/> is set; with neither,
/// this is the company-wide default. Resolution is most-specific-wins:
/// posting → the posting's department → company-wide.</para>
///
/// <para>The department is the default level <i>because</i> that is where comparison means
/// something: the criteria that make two engineers comparable make an engineer and a
/// salesperson incomparable. A single company-wide list forces departments to score against
/// fields that don't apply to them, and per-posting-only lists start empty every time, which
/// is how you get the inconsistency 3.3 exists to remove.</para>
/// </summary>
public class ScorecardTemplate : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Set for a department-level template. Mutually exclusive with
    /// <see cref="JobPostingId"/>; both null means the company-wide default.</summary>
    public Guid? DepartmentId { get; set; }

    /// <summary>Set for a single posting that needs its own criteria. Mutually exclusive
    /// with <see cref="DepartmentId"/>.</summary>
    public Guid? JobPostingId { get; set; }

    /// <summary>Deactivated rather than deleted: interviews reference the template they were
    /// scored against, and deleting it would orphan them.</summary>
    public bool IsActive { get; set; } = true;
}
