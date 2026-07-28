namespace RecruitOps.Application.Common;

/// <summary>How the current user reaches a job application.</summary>
public enum ApplicationReachKind
{
    /// <summary>Through department scoping (ADR-0003) — or an unscoped company-wide role.</summary>
    Department,

    /// <summary>Only because they are on an interview panel for this application
    /// (ADR-0017 §4). Read-only, and confined to this one application.</summary>
    Participation,
}

/// <summary>What the current user may do with one job application, and why.</summary>
/// <param name="ApplicationId">The application reached.</param>
/// <param name="JobPostingId">Its posting.</param>
/// <param name="DepartmentId">The posting's department — the scoping axis.</param>
/// <param name="Kind">Which rule granted the reach.</param>
public record ApplicationReach(
    Guid ApplicationId,
    Guid JobPostingId,
    Guid DepartmentId,
    ApplicationReachKind Kind)
{
    /// <summary>True only for department reach. Gates writes <b>to the process</b>:
    /// rescheduling a round, changing a panel, moving a stage. A panel member from another
    /// department can read the application they are interviewing for but cannot do any of
    /// those (ADR-0017 §4).
    ///
    /// <para><b>It does not gate a panel member's own contributions.</b> Writing their
    /// scorecard and posting to the note thread is the job they were added to do, so
    /// <c>ScorecardService</c> and <c>NoteService.CreateAsync</c> deliberately do not consult
    /// this flag — they gate on participation instead. An earlier version of this comment
    /// said participation could not "touch anything else", which contradicted both the
    /// shipped behaviour and the test that pins it
    /// (<c>A_Panel_Member_Can_Read_And_Join_The_Thread</c>).</para></summary>
    public bool CanWrite => Kind == ApplicationReachKind.Department;
}

/// <summary>Resolves whether the current user may reach a job application at all.
///
/// <para><b>Why this exists as one interface instead of a check in each service.</b>
/// Interviews, scorecards and notes all hang off a job application and all need the same
/// two-clause rule: department access (ADR-0003) <i>or</i> panel participation
/// (ADR-0017 §4). The recurring bug in this repo is a guard added to two of three sibling
/// methods — an ownership check that reached edit and cancel but not submit let an approver
/// push someone else's draft into a chain. Three services re-deriving this rule is the same
/// shape of mistake waiting to happen, so there is one implementation and everything calls
/// it.</para>
///
/// <para>Returns <c>null</c> — not an exception, not false — when the application does not
/// exist <i>or</i> is out of reach, so callers translate both to <b>404</b> and existence is
/// never leaked (the established convention; see <c>RequisitionService</c>).</para>
/// </summary>
public interface IApplicationAccess
{
    /// <summary>Resolve reach to an application by its id.</summary>
    Task<ApplicationReach?> ResolveAsync(Guid jobApplicationId, CancellationToken ct = default);

    /// <summary>Resolve reach to the application behind an interview.</summary>
    Task<ApplicationReach?> ResolveByInterviewAsync(Guid interviewId, CancellationToken ct = default);

    /// <summary>Resolve reach for <b>some other user</b>, not the caller.
    ///
    /// <para>Exists for @mention resolution, which has to ask "could <i>they</i> see this?"
    /// before turning a handle into a mention — otherwise a mention is a disclosure channel,
    /// and once Module 7 delivers notifications, a delivery mechanism for it.</para>
    ///
    /// <para>It lives here rather than in <c>NoteService</c> because that is where it used to
    /// live, spelled out by hand, and it had already drifted: it tested
    /// <c>role is UserRole.HiringManager</c> directly, so it granted an Approver reach to
    /// every application in the company — the exact case the method's own doc comment gave as
    /// the thing it prevented.</para>
    /// </summary>
    Task<ApplicationReach?> ResolveForUserAsync(
        Guid userId, Guid jobApplicationId, CancellationToken ct = default);

    /// <summary>True when the current user is on this interview's panel. Distinct from
    /// <see cref="ResolveAsync"/>: a recruiter reaches every application in the company but
    /// is only <i>blinded</i> to the panel's scores if they are on the panel themselves
    /// (ADR-0017 §3).</summary>
    Task<bool> IsParticipantAsync(Guid interviewId, CancellationToken ct = default);
}
