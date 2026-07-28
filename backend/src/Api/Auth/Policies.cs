namespace RecruitOps.Api.Auth;

/// <summary>Authorization policy names.</summary>
public static class Policies
{
    /// <summary>Talent acquisition staff who own the hiring process: Admin, HrDirector,
    /// Recruiter.
    ///
    /// <para>Excludes HiringManager, who is department-scoped (ADR-0003), and Approver, who
    /// is <b>not</b> department-scoped but has no business in the pipeline at all
    /// (ADR-0018) — two different reasons for the same exclusion. An earlier version of this
    /// comment said both were department-scoped; that was wrong about Approver and read like
    /// a guarantee the policy layer does not give.</para></summary>
    public const string RecruitmentStaff = "RecruitmentStaff";

    /// <summary>Any authenticated internal user, including department-scoped roles and
    /// Approver.
    ///
    /// <para>Endpoints using this MUST apply the scoping predicate themselves (ADR-0003) —
    /// and for anything hanging off a candidate, that means <c>IApplicationAccess</c>, which
    /// applies the candidate-data exclusion (ADR-0018) as well. This policy is not access
    /// control; it only establishes that somebody is logged in and internal.</para></summary>
    public const string InternalUser = "InternalUser";

    /// <summary>Administrators only.</summary>
    public const string AdminOnly = "AdminOnly";
}
