namespace RecruitOps.Application.Common;

/// <summary>Resolves which departments the current user may see (ADR-0003).
/// <para>Deliberately NOT an EF global query filter: the rule is conditional per role,
/// and Candidates are only indirectly departmental (through their applications), which
/// a static filter cannot express. Callers apply the predicate explicitly.</para></summary>
public interface IDepartmentAccess
{
    /// <summary>Department ids the current user may access. Only meaningful when
    /// <see cref="ICurrentUser.IsDepartmentScoped"/> is true — unscoped roles see everything.</summary>
    Task<IReadOnlyCollection<Guid>> AccessibleDepartmentIdsAsync(CancellationToken ct = default);

    /// <summary>True when the user may act on the given department.</summary>
    Task<bool> CanAccessAsync(Guid departmentId, CancellationToken ct = default);
}
