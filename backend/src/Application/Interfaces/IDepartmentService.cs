using RecruitOps.Application.DTOs;

namespace RecruitOps.Application.Interfaces;

public interface IDepartmentService
{
    /// <summary>Departments the current user can work in, ordered by name.
    ///
    /// <para>A Hiring Manager gets only their own (ADR-0003) — this list feeds the
    /// department picker on the new-requisition form, and showing them departments they
    /// cannot actually create in would offer a choice the API then rejects.</para></summary>
    Task<IReadOnlyList<DepartmentListItemDto>> GetDepartmentsAsync(CancellationToken ct = default);

    /// <summary>Every department, with member and open-requisition counts. Admin surface.</summary>
    Task<IReadOnlyList<DepartmentDetailDto>> GetForAdminAsync(CancellationToken ct = default);

    /// <summary>Throws InvalidOperationException if the name is already taken.</summary>
    Task<DepartmentDetailDto> CreateAsync(CreateDepartmentRequest request, CancellationToken ct = default);

    Task<DepartmentDetailDto?> UpdateAsync(Guid id, UpdateDepartmentRequest request, CancellationToken ct = default);

    /// <summary>Departments are never deleted — requisitions, postings and the audit trail
    /// point at them. Deactivating stops new work being raised in one while leaving the
    /// history intact. Throws if the department still has requisitions in flight.</summary>
    Task<DepartmentDetailDto?> SetActiveAsync(Guid id, bool isActive, CancellationToken ct = default);

    /// <summary>Every user, flagged with whether they belong to this department.
    /// Returns the whole roster because that is what an admin assigns from.</summary>
    Task<IReadOnlyList<DepartmentMemberDto>?> GetMembersAsync(Guid id, CancellationToken ct = default);

    /// <summary>Replaces the department's member list. This is the ADR-0003 access-control
    /// axis, so an unknown user id is an error rather than something to skip quietly.</summary>
    Task<IReadOnlyList<DepartmentMemberDto>?> SetMembersAsync(
        Guid id, SetDepartmentMembersRequest request, CancellationToken ct = default);
}
