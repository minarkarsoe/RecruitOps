using System.ComponentModel.DataAnnotations;

namespace RecruitOps.Application.DTOs;

/// <summary>A department plus the counts an admin needs before changing it.
///
/// <para><c>MemberCount</c> and <c>OpenRequisitionCount</c> are here because deactivating a
/// department that still has members or live requisitions is usually a mistake, and the only
/// way to notice is to be told at the moment of deciding.</para>
/// </summary>
public record DepartmentDetailDto(
    Guid Id,
    string Name,
    string? Code,
    bool IsActive,
    int MemberCount,
    int OpenRequisitionCount);

/// <summary>A user who can be assigned to a department, and whether they currently are.</summary>
/// <param name="Email">Only for a caller who holds <c>users:read</c>, which is what reading
/// addresses means everywhere else (ADR-0019). Admin and HR Director hold both by default; a custom
/// role given <c>settings:read</c> alone sees names and roles, enough to tell people apart.</param>
public record DepartmentMemberDto(
    Guid UserId,
    string DisplayName,
    string? Email,
    string Role,
    bool IsMember);

public record CreateDepartmentRequest
{
    [Required, StringLength(200, MinimumLength = 2)]
    public string Name { get; init; } = string.Empty;

    /// <summary>Short internal code (e.g. "SALES-MM"). Optional — plenty of companies
    /// don't have one, and inventing a required field they must fill is friction.</summary>
    [StringLength(50)]
    public string? Code { get; init; }
}

public record UpdateDepartmentRequest
{
    [Required, StringLength(200, MinimumLength = 2)]
    public string Name { get; init; } = string.Empty;

    [StringLength(50)]
    public string? Code { get; init; }
}

/// <summary>Replaces a department's whole member list.
///
/// <para>Set-the-whole-list rather than add/remove one at a time: department membership is
/// the access-control axis (ADR-0003), and an admin editing it should be looking at the
/// complete list they are committing to, not issuing deltas against a state they can't see.</para>
/// </summary>
public record SetDepartmentMembersRequest
{
    /// <summary>The whole membership. Bounded only so one request cannot be an arbitrarily
    /// large IN list; far above any real department.</summary>
    [Required, MaxLength(5000)]
    public Guid[] UserIds { get; init; } = [];
}
