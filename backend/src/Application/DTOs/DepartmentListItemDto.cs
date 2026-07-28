namespace RecruitOps.Application.DTOs;

/// <summary>A row in the department list (org units that own requisitions).</summary>
public record DepartmentListItemDto(Guid Id, string Name, string? Code, bool IsActive);
