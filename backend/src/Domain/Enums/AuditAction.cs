namespace RecruitOps.Domain.Enums;

/// <summary>What an audit-log entry records (ADR-0027). Stored by name, so reordering is safe;
/// renaming a member orphans every row already written under the old name — add, don't rename.</summary>
public enum AuditAction
{
    SignInSucceeded,
    SignInFailed,
    /// <summary>A rotated (revoked) refresh token was presented again — someone else holds a copy.
    /// Every session the user had is ended (ADR-0028).</summary>
    RefreshTokenReused,

    UserCreated,
    /// <summary>A rename — the role did not change.</summary>
    UserUpdated,
    UserRoleChanged,
    UserDeactivated,
    UserReactivated,
    /// <summary>Who belongs to a department changed — the ADR-0003 axis: it decides which
    /// requisitions a department-scoped user reaches.</summary>
    DepartmentMembersChanged,
    /// <summary>A department was created, renamed, deactivated or reactivated. Not access by
    /// itself, but the unit access is granted in — and since 2026-10-17 delegable through
    /// <c>settings:update</c>, so who did it has to be answerable.</summary>
    DepartmentCreated,
    DepartmentUpdated,
    DepartmentDeactivated,
    DepartmentReactivated,

    /// <summary>Two candidate records folded into one (Module 2.7). Not an access change, but not
    /// reversible either — who merged which two has to be answerable. Ids only, never names.</summary>
    CandidatesMerged,

    /// <summary>Two candidate records marked "not the same person" (Module 2.7): it stops them being
    /// merged for good, so it is answerable too. Ids only, never names.</summary>
    CandidatesKeptSeparate,

    /// <summary>The company's offer policy changed (ADR-0030): whether Hiring Managers see offered
    /// salaries — an access change — and whether every offer needs approval — a financial control.</summary>
    OfferSettingsChanged,

    RoleCreated,
    RoleUpdated,
    RoleDeleted,

    /// <summary>The grantor ceiling turned a request away (ADR-0021): someone tried to give access
    /// they do not hold, or to manage someone above them.</summary>
    AccessChangeRefused,
}
