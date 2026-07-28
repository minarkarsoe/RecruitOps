namespace RecruitOps.Domain.Enums;

/// <summary>Candidate pipeline vocabulary for the in-house model.
/// Fixed vocabulary — must stay in sync with packages/types (frontend) and the
/// design system's status pill (§5.2). Do not invent new labels.</summary>
public enum PipelineStatus
{
    /// <summary>Added by a recruiter; has not applied.</summary>
    Sourced,

    /// <summary>Came in through an application form or sourcing channel.</summary>
    Applied,

    Screening,
    Shortlisted,
    Interview,
    Offer,
    Hired,
    Rejected,
}
