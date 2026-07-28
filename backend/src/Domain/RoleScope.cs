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
    /// </summary>
    public static bool IsDepartmentScoped(UserRole role) => role is UserRole.HiringManager;

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
    /// </summary>
    public static bool IsExcludedFromCandidateData(UserRole role) => role is UserRole.Approver;

    /// <summary>Parses a role name, returning null for anything unrecognised.
    ///
    /// <para>Callers must treat null as the <b>most restrictive</b> answer, not the least:
    /// an unparseable role is a misconfiguration, and a misconfiguration should not read like
    /// an Admin. The properties on <c>ICurrentUser</c> do exactly that.</para>
    /// </summary>
    public static UserRole? Parse(string? role) =>
        Enum.TryParse<UserRole>(role, ignoreCase: false, out var parsed) ? parsed : null;
}
