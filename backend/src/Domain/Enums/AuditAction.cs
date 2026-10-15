namespace RecruitOps.Domain.Enums;

/// <summary>What an audit-log entry records (ADR-0027). Stored by name, so reordering is safe;
/// renaming a member orphans every row already written under the old name — add, don't rename.</summary>
public enum AuditAction
{
    SignInSucceeded,
    SignInFailed,

    UserCreated,
    /// <summary>A rename — the role did not change.</summary>
    UserUpdated,
    UserRoleChanged,
    UserDeactivated,
    UserReactivated,

    RoleCreated,
    RoleUpdated,
    RoleDeleted,

    /// <summary>The grantor ceiling turned a request away (ADR-0021): someone tried to give access
    /// they do not hold, or to manage someone above them.</summary>
    AccessChangeRefused,
}
