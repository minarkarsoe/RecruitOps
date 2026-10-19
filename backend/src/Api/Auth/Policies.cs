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

    /// <summary><see cref="InternalUser"/> plus <b>Interviewer</b> — for the Module 3 reads, a panel
    /// member's own scorecard, and an application's notes: the endpoints whose service decides
    /// reach per application through <c>IApplicationAccess</c>, which gives a role excluded from
    /// candidate data exactly the applications whose panel it sits on (ADR-0017 §4).
    ///
    /// <para>Interviewer was in no role policy at all until 2026-10-19, so the role's one screen
    /// answered 403 — including for holders of a deactivated custom role, which carry it (ADR-0021).
    /// Product owner's decision: they see and score their own panels, and nothing else. Never widen
    /// <see cref="InternalUser"/> instead — dozens of endpoints rely on it excluding Interviewer,
    /// several of them guarded only by that and a permission.</para></summary>
    public const string PanelAccess = "PanelAccess";

    /// <summary>Administrators only.</summary>
    public const string AdminOnly = "AdminOnly";

    /// <summary>A role that reaches every department on both axes — requisitions and candidates:
    /// <c>RoleScope</c> says it is neither department-scoped nor excluded from candidate data. The
    /// one definition of both stays there.
    ///
    /// <para>Department and approval-chain administration need it <em>on top of</em> their
    /// permission. Both move the ADR-0003 axis itself: department membership decides which
    /// requisitions — and candidates — a scoped role reaches, and whoever is named on a chain can open
    /// the requisitions it governs. A Hiring Manager base holding <c>settings:update</c> could
    /// otherwise put themselves in every department, or on every chain, and leave the reach the
    /// grantor ceiling approved (security review, 2026-10-17, M2). An Approver base is not scoped,
    /// but reaches no candidate: moving a Hiring Manager into every department would hand out
    /// candidate reach it does not hold (second review, 2026-10-19, F1). Not a permission check: a
    /// permission says what someone may do; this says only a role that already reaches everything
    /// may do it.</para></summary>
    public const string CompanyWide = "CompanyWide";
}
