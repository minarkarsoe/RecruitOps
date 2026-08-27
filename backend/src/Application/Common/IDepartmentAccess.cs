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

    /// <summary>True when the user may act on the given department <b>on the requisition axis</b> —
    /// requisitions, postings, approval chains, JD and scorecard templates.
    ///
    /// <para>⚠️ <b>This is NOT the right question for candidate data.</b> It answers "does this role
    /// work across departments", and an <c>Approver</c> does, deliberately (ADR-0003: a Finance
    /// approver signs off the Sales headcount request). Asked about a <i>candidate</i>, that same
    /// <c>true</c> hands them every candidate in the company. For anything that reaches a
    /// candidate, an application, a CV, a scorecard or a pipeline stage, call
    /// <see cref="CanReachCandidatesInAsync"/> instead.</para>
    /// </summary>
    Task<bool> CanAccessAsync(Guid departmentId, CancellationToken ct = default);

    /// <summary>Department scoping (ADR-0003) <b>and</b> the candidate-data exclusion (ADR-0018),
    /// through one door. Use this for every question that touches candidate data.
    ///
    /// <para><b>Why this is on the interface rather than a private helper in each service:</b>
    /// it was a private helper in each service, and that is precisely how the same bug shipped
    /// three times — <c>NoteService</c>, then <c>PipelineService</c>, then <c>BulkResumeService</c>,
    /// the last one written <i>after</i> ADR-0018 was documented and with the corrected version
    /// sitting one file away. Each service was asked to remember a two-part rule and the
    /// forgettable part is the half that is not about departments. Confirmed against the running
    /// API on 2026-08-26: an Approver could <c>POST</c> fifty CVs into any posting in the company
    /// and read the batch back, returning <b>200 OK</b>.</para>
    ///
    /// <para>Roles excluded from candidate data reach an individual application only by sitting on
    /// its panel (ADR-0017 §4), which <c>IApplicationAccess</c> resolves. Nothing here grants
    /// standing reach.</para>
    /// </summary>
    Task<bool> CanReachCandidatesInAsync(Guid departmentId, CancellationToken ct = default);
}
