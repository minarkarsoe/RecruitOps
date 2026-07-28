namespace RecruitOps.Application.Common;

/// <summary>The authenticated principal, resolved from JWT claims.</summary>
public interface ICurrentUser
{
    /// <summary>User id from the `sub` claim; null when unauthenticated.</summary>
    Guid? UserId { get; }

    /// <summary>Role name from the role claim; null when unauthenticated.</summary>
    string? Role { get; }

    /// <summary>True when this role only sees its own departments on the requisition /
    /// posting axis (ADR-0003). Currently HiringManager; Admin/HrDirector/Recruiter/Approver
    /// work across all departments.
    /// <para>Decided by <c>RoleScope.IsDepartmentScoped</c> — do not re-express the role
    /// list anywhere else.</para></summary>
    bool IsDepartmentScoped { get; }

    /// <summary>True when this role has no standing reach into <b>candidate</b> data —
    /// applications, pipeline, interviews, scorecards, notes (ADR-0018). Currently Approver.
    ///
    /// <para>A separate axis from <see cref="IsDepartmentScoped"/> on purpose: an Approver is
    /// company-wide for requisitions and reaches no candidate at all, so neither flag alone
    /// describes them. They still reach one application by being on its panel
    /// (ADR-0017 §4).</para></summary>
    bool IsExcludedFromCandidateData { get; }
}
