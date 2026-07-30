namespace RecruitOps.Domain.Enums;

/// <summary>RBAC roles for an in-house talent acquisition team (Module 7.1).
/// See docs/decisions/ADR-0003-department-scoping.md — HiringManager is department-scoped.</summary>
public enum UserRole
{
    /// <summary>Global system super administrator with full system-wide privileges.</summary>
    SuperAdmin,

    /// <summary>System administrator: settings, RBAC, integrations.</summary>
    Admin,

    /// <summary>Management / HR Director: all reports, budget and plan approval.</summary>
    HrDirector,

    /// <summary>In-house recruiter: full pipeline across all departments.</summary>
    Recruiter,

    /// <summary>Department manager: raises requisitions; sees only their own departments.</summary>
    HiringManager,

    /// <summary>Dept Head / Finance / HR acting in an approval chain.</summary>
    Approver,

    /// <summary>Interviewer evaluating candidates on interview panels.</summary>
    Interviewer,
}
