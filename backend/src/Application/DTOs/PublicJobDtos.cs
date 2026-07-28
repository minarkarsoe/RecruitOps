using System.ComponentModel.DataAnnotations;

namespace RecruitOps.Application.DTOs;

/// <summary>What an anonymous visitor sees on the public job page.
///
/// <para>Deliberately narrower than <see cref="JobPostingDetailDto"/>. There is no
/// department id, no requisition id, no headcount and no salary unless the posting opted in
/// — those are internal facts, and "just reuse the internal DTO" is how they end up on a page
/// indexed by Facebook.</para>
/// </summary>
public record PublicJobDto(
    string Title,
    string Description,
    string? Location,
    string EmploymentType,
    string CompanyName,
    string? SalaryRange,
    string? ApplicationFormFieldsJson,
    bool IsOpen);

/// <summary>An application submitted from the public page. Every field is anonymous,
/// untrusted input, so everything is length-capped.</summary>
public record SubmitApplicationRequest
{
    [Required, StringLength(200, MinimumLength = 1)]
    public string FullName { get; init; } = string.Empty;

    [EmailAddress, StringLength(256)]
    public string? Email { get; init; }

    [Phone, StringLength(30)]
    public string? Phone { get; init; }

    [StringLength(4000)]
    public string? CoverNote { get; init; }

    /// <summary>Answers to the posting's custom fields, as a JSON object.</summary>
    [StringLength(8000)]
    public string? CustomFieldsJson { get; init; }
}

/// <summary>Confirmation shown to the applicant.
///
/// <para>It carries no candidate or application id. Returning one would let anyone who
/// applied start guessing at other people's records, and the applicant has no way to use it
/// — there is no account to log into.</para>
/// </summary>
public record SubmitApplicationResponse(string Message);
