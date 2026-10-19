using RecruitOps.Domain.Enums;

namespace RecruitOps.Domain;

/// <summary>What a role may reach, decided in one place.
///
/// <para><b>Why this exists.</b> The scoping rule used to be written twice: once in
/// <c>CurrentUser.IsDepartmentScoped</c> against the role <i>claim</i>, and once in
/// <c>NoteService</c> as a bare <c>role is UserRole.HiringManager</c> against a row from the
/// database. Two copies of one rule is the recurring bug in this repo — the second copy does
/// not follow when the first is corrected. Both now call these predicates, so there is one
/// answer and it moves as a unit.</para>
///
/// <para>Note that the two axes below are <b>different questions</b> and deliberately have
/// different answers for <c>Approver</c>. See ADR-0018.</para>
/// </summary>
public static class RoleScope
{
    /// <summary>True when the role only sees its own departments on the
    /// <b>requisition / posting</b> axis (ADR-0003).
    ///
    /// <para><c>Approver</c> is deliberately <i>not</i> scoped here: an approver must be able
    /// to see the requisition they have been asked to approve, and an approval chain
    /// routinely crosses departments (Finance signs off on a Sales headcount).</para>
    ///
    /// <para><c>Interviewer</c> is scoped (2026-10-15): it is the role with no standing reach — only
    /// the panels it sits on (2026-10-19) — and the grantor ceiling relies on it being the narrowest
    /// on every axis. It used to be
    /// company-wide here, which no role policy exposed — until a custom role on an Interviewer
    /// base was given <c>requisitions:read</c>, a permission-gated endpoint, and saw every
    /// department (security review of ADR-0021 option A).</para>
    /// </summary>
    public static bool IsDepartmentScoped(UserRole role) => role is UserRole.HiringManager or UserRole.Interviewer;

    /// <summary>True when the role has <b>no standing reach into candidate data at all</b> —
    /// applications, their pipeline, interviews, scorecards and notes (ADR-0018).
    ///
    /// <para><c>Approver</c> is excluded. "An approver must see what they are approving"
    /// (ADR-0003) is an argument about <i>requisitions</i> — a headcount request, a budget
    /// line, a job title. It was never an argument for reading a named candidate's interview
    /// debrief, and Module 3 turned the one into the other by accident: because an Approver is
    /// not department-scoped, <c>CanAccessAsync</c> returned true for every department, which
    /// handed them every application in the company.</para>
    ///
    /// <para>An excluded role is not locked out for good — it reaches an individual
    /// application by sitting on that application's interview panel, exactly like a Hiring
    /// Manager from another department does (ADR-0017 §4).</para>
    ///
    /// <para><c>Interviewer</c> is excluded for the same reason it is department-scoped: it reaches
    /// an application only by sitting on its panel, which is what an interviewer does.</para>
    /// </summary>
    public static bool IsExcludedFromCandidateData(UserRole role) => role is UserRole.Approver or UserRole.Interviewer;

    /// <summary>True when a panel seat opens <b>only the round sat on</b> — its detail, its panel's
    /// evaluations, the notes pinned to it — rather than the whole application, as ADR-0017 §4
    /// grants every other role. A third axis, and like the two above it only narrows.
    ///
    /// <para><c>Interviewer</c> only (product owner, 2026-10-19): the role exists to evaluate one
    /// conversation, and reading the candidate's other rounds — their evaluations included, which
    /// the per-round blind rule does not hide — is more than that. A Hiring Manager or Approver on a
    /// panel keeps the application-wide seat.</para>
    /// </summary>
    public static bool IsLimitedToOwnRounds(UserRole role) => role is UserRole.Interviewer;

    /// <summary>Parses a role name, returning null for anything unrecognised.
    ///
    /// <para>Callers must treat null as the <b>most restrictive</b> answer, not the least:
    /// an unparseable role is a misconfiguration, and a misconfiguration should not read like
    /// an Admin. The properties on <c>ICurrentUser</c> do exactly that.</para>
    /// </summary>
    /// <para>Exact member names only. <c>Enum.TryParse</c> also accepted numeric strings, values no
    /// member has, and comma-joined names — and an undefined value is neither scoped nor
    /// excluded, the permissive answer (security review, 2026-10-15).</para>
    public static UserRole? Parse(string? role) =>
        role is not null && RoleNames.Contains(role) ? Enum.Parse<UserRole>(role) : null;

    private static readonly HashSet<string> RoleNames = new(Enum.GetNames<UserRole>(), StringComparer.Ordinal);

    /// <summary>The base roles a custom role may be built on (ADR-0021 option A): every system
    /// role below Admin. Admin reach is given by assigning the Admin system role, where the user
    /// directory shows it; SuperAdmin is never given through the API at all.</summary>
    public static IReadOnlyList<UserRole> AssignableBaseRoles { get; } = new[]
    {
        UserRole.HrDirector, UserRole.Recruiter, UserRole.HiringManager, UserRole.Approver, UserRole.Interviewer,
    };

    /// <summary>True when <paramref name="actor"/>'s reach includes everything
    /// <paramref name="target"/>'s does — the grantor ceiling's question (ADR-0021 option A):
    /// <c>Admin ⊇ HrDirector ⊇ Recruiter ⊇ {HiringManager, Approver} ⊇ Interviewer</c>.
    ///
    /// <para>Read off the role policies plus the two scopes above: Admin alone passes
    /// <c>AdminOnly</c>; HrDirector and Recruiter share <c>RecruitmentStaff</c>, and HrDirector adds
    /// company-wide authority over others' requisitions (<c>RequisitionService</c>); HiringManager
    /// (department-scoped) and Approver (no candidate data) are each narrower than Recruiter and
    /// neither covers the other; Interviewer passes only <c>PanelAccess</c> — which every other role
    /// passes too, and which reaches nothing but the caller's own panels (ADR-0021 amendment,
    /// 2026-10-19) — and is both department-scoped and excluded from candidate data. <c>RoleScopeCoversTests</c> checks this order against the
    /// policies the API registers and both scopes.</para></summary>
    public static bool Covers(UserRole actor, UserRole target) =>
        actor == target
        || actor == UserRole.SuperAdmin
        || DirectlyBelow.TryGetValue(actor, out var below) && below.Any(b => Covers(b, target));

    private static readonly Dictionary<UserRole, UserRole[]> DirectlyBelow = new()
    {
        [UserRole.Admin] = new[] { UserRole.HrDirector },
        [UserRole.HrDirector] = new[] { UserRole.Recruiter },
        [UserRole.Recruiter] = new[] { UserRole.HiringManager, UserRole.Approver },
        [UserRole.HiringManager] = new[] { UserRole.Interviewer },
        [UserRole.Approver] = new[] { UserRole.Interviewer },
    };
}
