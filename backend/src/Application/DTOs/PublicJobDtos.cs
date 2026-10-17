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

    // A field the applicant skipped arrives from the browser as "", never absent — the form posts
    // its whole state. "" means "not given", so it becomes null here, as the JSON is read and
    // before validation runs: [EmailAddress] and [Phone] accept null but reject "". Until
    // 2026-10-16 that turned away everyone who gave only one way to reach them — which the form
    // explicitly invites — with a 400 about a field they had left empty. What was typed is still
    // validated; the service normalises it further (ContactNormalizer).
    [EmailAddress, StringLength(256)]
    public string? Email { get; init => field = NullIfBlank(value); }

    [Phone, StringLength(30)]
    public string? Phone { get; init => field = NullIfBlank(value); }

    [StringLength(4000)]
    public string? CoverNote { get; init => field = NullIfBlank(value); }

    /// <summary>Answers to the posting's custom fields, as a JSON object.</summary>
    [StringLength(8000)]
    public string? CustomFieldsJson { get; init; }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}

/// <summary>Confirmation shown to the applicant.
///
/// <para>It carries no candidate or application id. Returning one would let anyone who
/// applied start guessing at other people's records, and the applicant has no way to use it
/// — there is no account to log into.</para>
/// </summary>
public record SubmitApplicationResponse(string Message);
