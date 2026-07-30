namespace RecruitOps.Application.DTOs;

/// <summary>User representation for user directory and approval-chain builder UI.
/// Never exposes password hashes or other sensitive fields.</summary>
public record UserListItemDto(
    Guid Id,
    string Email,
    string DisplayName,
    string Role,
    Guid? RoleId = null,
    string? RoleName = null,
    bool IsActive = true,
    DateTimeOffset CreatedAt = default);

/// <summary>A user who can be picked for something — currently an interview panel.
///
/// <para><b>Narrower than <see cref="UserListItemDto"/> on purpose.</b> The panel picker
/// needs a name to show and an id to post; it does not need an email address. That full
/// directory stays <c>AdminOnly</c>, so widening the picker to recruitment staff does not
/// widen the directory with it. <c>Role</c> is kept because a scheduler choosing between two
/// people with similar names needs to tell a Recruiter from a Hiring Manager.</para>
/// </summary>
public record SelectableUserDto(
    Guid Id,
    string DisplayName,
    string Role);
