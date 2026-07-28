namespace RecruitOps.Application.DTOs;

public record JdTemplateDto(Guid Id, string Title, string Content, Guid? DepartmentId, bool IsActive);
