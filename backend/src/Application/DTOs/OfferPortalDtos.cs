using System.ComponentModel.DataAnnotations;

namespace RecruitOps.Application.DTOs;

// The candidate's side of an offer (Module 4.3, ADR-0030 §5a), drawn in design/public/offer-portal.html.
// No internal vocabulary crosses this boundary: no ids, no offer status names, no recruiter.

/// <summary>What the link alone shows: who the offer is from and where the code will go. Nothing about
/// the offer itself — the link is an address, the code is the credential.</summary>
/// <param name="State"><c>Open</c> (a code unlocks it) or <c>Expired</c>.</param>
/// <param name="MaskedEmail">Null when the application has no email address: no code can be sent.</param>
/// <param name="ContactEmail">Who to write to — the outbound mail's From address. Null if unset.</param>
public record PortalOfferLinkDto(
    string CompanyName,
    string State,
    string? MaskedEmail,
    DateOnly ExpiresOn,
    string? ContactEmail);

/// <summary>The offer, once unlocked.</summary>
/// <param name="State"><c>Open</c>, <c>Accepted</c> or <c>Declined</c>.</param>
/// <param name="LetterText">The letter as it will be signed — or, once accepted, exactly as it was.</param>
/// <param name="LetterSha256">Sent back on accepting, so the server can refuse if the letter changed in
/// between: what is recorded is what was read.</param>
public record PortalOfferDto(
    string CompanyName,
    string Position,
    string DepartmentName,
    decimal BaseSalary,
    decimal Allowances,
    DateOnly JoiningDate,
    int ProbationMonths,
    DateOnly ExpiresOn,
    string State,
    string LetterText,
    string LetterSha256,
    string? SignedName,
    DateTimeOffset? AnsweredAt,
    string? ContactEmail);

public record VerifyOfferCodeRequest
{
    [Required, StringLength(32)]
    public string Code { get; init; } = string.Empty;
}

public record AcceptOfferRequest
{
    [Required, StringLength(400)]
    public string TypedName { get; init; } = string.Empty;

    /// <summary>Must be true: the box "I have read the offer letter and I accept this offer".</summary>
    [Required]
    public bool? Consent { get; init; }

    [Required, StringLength(64, MinimumLength = 64)]
    public string LetterSha256 { get; init; } = string.Empty;
}

public record DeclineOfferRequest
{
    [StringLength(2000)]
    public string? Reason { get; init; }
}

/// <summary>A session opened by a correct code: the cookie's value and when it stops working.</summary>
public record PortalSession(string Token, DateTimeOffset ExpiresAt);
