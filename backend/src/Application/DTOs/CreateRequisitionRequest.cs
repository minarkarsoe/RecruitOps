using System.ComponentModel.DataAnnotations;

namespace RecruitOps.Application.DTOs;

public record CreateRequisitionRequest
{
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
