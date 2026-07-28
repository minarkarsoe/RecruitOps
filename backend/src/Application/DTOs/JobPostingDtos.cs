using System.ComponentModel.DataAnnotations;

namespace RecruitOps.Application.DTOs;

/// <summary>Row in the internal postings list.</summary>
/// <param name="PublicToken">Null until published — there is nothing to share before then.</param>
public record JobPostingListItemDto(
    Guid Id,
    Guid DepartmentId,
    string DepartmentName,
    Guid RequisitionId,
    string Title,
    string Status,
    string EmploymentType,
    string? Location,
    int Headcount,
    DateTimeOffset? PostedAt,
    DateTimeOffset? ClosedAt,
    string? PublicToken,
    int ApplicationCount);

public record JobPostingDetailDto(
    Guid Id,
    Guid DepartmentId,
    string DepartmentName,
    Guid RequisitionId,
    string Title,
    string Description,
    string Status,
    string EmploymentType,
    string? Location,
    int Headcount,
    decimal? SalaryMin,
    decimal? SalaryMax,
    bool ShowSalary,
    string? ApplicationFormFieldsJson,
    DateTimeOffset? PostedAt,
    DateTimeOffset? ClosedAt,
    string? PublicToken,
    int ApplicationCount);

/// <summary>Creating a posting takes only the requisition — title and description are copied
/// from it so the first version of the advert always matches what was approved. Editing
/// afterwards is a separate, deliberate act.</summary>
public record CreateJobPostingRequest
{
    [Required]
    public Guid RequisitionId { get; init; }
}

public record UpdateJobPostingRequest
{
    [Required, StringLength(200, MinimumLength = 2)]
    public string Title { get; init; } = string.Empty;

    [Required]
    public string Description { get; init; } = string.Empty;

    [StringLength(200)]
    public string? Location { get; init; }

    /// <summary>One of the <c>EmploymentType</c> names.</summary>
    [Required, StringLength(20)]
    public string EmploymentType { get; init; } = "FullTime";

    [Range(1, 1000)]
    public int Headcount { get; init; } = 1;

    [Range(0, double.MaxValue)]
    public decimal? SalaryMin { get; init; }

    [Range(0, double.MaxValue)]
    public decimal? SalaryMax { get; init; }

    /// <summary>Off by default at the entity level: the requisition's budget is internal,
    /// and publishing it accidentally would leak the company's pay bands.</summary>
    public bool ShowSalary { get; init; }

    /// <summary>Customer-defined application-form fields (Module 2.2), as a JSON array.</summary>
    public string? ApplicationFormFieldsJson { get; init; }
}
