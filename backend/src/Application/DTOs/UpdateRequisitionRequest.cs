using System.ComponentModel.DataAnnotations;

namespace RecruitOps.Application.DTOs;

/// <summary>Body for editing a Draft requisition. Deliberately a separate type from
/// <see cref="CreateRequisitionRequest"/> even though the fields match today — what you may
/// set at creation and what you may later change are different questions, and merging them
/// would make the first divergence a breaking change on both.</summary>
public record UpdateRequisitionRequest
{
    /// <summary>Moving a Draft to another department is allowed, but the caller must be able
    /// to reach BOTH the current and the target department — otherwise it would be a way to
    /// push a requisition somewhere you cannot see (ADR-0003).</summary>
    [Required]
    public Guid DepartmentId { get; init; }

    [Required, StringLength(200, MinimumLength = 2)]
    public string Title { get; init; } = string.Empty;

    public string JobDescription { get; init; } = string.Empty;

    [Range(1, 1000)]
    public int Headcount { get; init; } = 1;

    [Range(0, double.MaxValue)]
    public decimal? SalaryBudget { get; init; }
}
