using System.ComponentModel.DataAnnotations;

namespace RecruitOps.Application.DTOs;

public record CreateJdTemplateRequest
{
    [Required, StringLength(200, MinimumLength = 2)]
    public string Title { get; init; } = string.Empty;

    [Required, MinLength(1)]
    public string Content { get; init; } = string.Empty;

    /// <summary>Null = available to every department.</summary>
    public Guid? DepartmentId { get; init; }
}
