using System.ComponentModel.DataAnnotations;

namespace RecruitOps.Application.DTOs;

// Module 4.3 pre-boarding (ADR-0030 §5b). Two audiences: the candidate on the offer link (Portal*), and
// recruitment staff on the review screen. Neither ever carries a whole account number — only the
// recruiter's audited Reveal does.

// ── The candidate's side ────────────────────────────────────────────────────

/// <param name="Id">The candidate's own file, so they can remove it. Not an internal record of anything else.</param>
public record PortalFileDto(Guid Id, string FileName, long SizeBytes);

/// <param name="AccountNumberMasked">Masked (<c>•••• 4471</c>) even to the candidate: retyped to change.</param>
public record PortalBankDto(string? BankName, string? AccountName, string? AccountNumberMasked);

/// <param name="Kind"><c>NrcFront</c>, <c>NrcBack</c>, <c>BankAccount</c>, <c>Education</c>, <c>Photo</c>.</param>
/// <param name="Status"><c>Missing</c>, <c>Submitted</c> (being checked), <c>Approved</c> (checked) or
/// <c>Returned</c> (sent back — <paramref name="Message"/> says why).</param>
/// <param name="Message">The recruiter's words, only while the document is returned.</param>
public record PortalDocumentDto(
    string Kind,
    bool Required,
    string Status,
    bool Complete,
    string? Message,
    IReadOnlyList<PortalFileDto> Files,
    PortalBankDto? Bank);

/// <param name="OpenUntil">The last day documents can be sent or changed — the joining date.</param>
/// <param name="Sent">Sent for checking, with nothing changed or sent back since: the page shows the
/// "sent" state.</param>
/// <param name="CanSend">Every required item complete, none sent back unchanged, and something new since
/// the last time.</param>
public record PortalDocumentsDto(
    DateOnly OpenUntil,
    DateTimeOffset? SignedAt,
    string? MaskedEmail,
    IReadOnlyList<PortalDocumentDto> Documents,
    int RequiredItems,
    int RequiredItemsReady,
    bool Sent,
    bool CanSend);

public record SaveBankDetailsRequest
{
    [Required, StringLength(400)]
    public string BankName { get; init; } = string.Empty;

    [Required, StringLength(400)]
    public string AccountName { get; init; } = string.Empty;

    [Required, StringLength(100)]
    public string AccountNumber { get; init; } = string.Empty;
}

// ── The recruiter's side ────────────────────────────────────────────────────

public record PreboardingFileDto(Guid Id, string FileName, string ContentType, long SizeBytes, DateTimeOffset UploadedAt);

/// <param name="AccountNumberMasked">The last four digits only; <c>POST …/bank/reveal</c> reads it whole,
/// and that is audited.</param>
public record PreboardingBankDto(string? BankName, string? AccountName, string? AccountNumberMasked);

/// <param name="Revision">Sent back with Approve or Send back: refused if the candidate changed the
/// document since it was shown.</param>
public record PreboardingDocumentDto(
    string Kind,
    string Label,
    bool Required,
    string Status,
    bool Complete,
    int Revision,
    DateTimeOffset? ChangedAt,
    DateTimeOffset? ReviewedAt,
    string? ReviewedByName,
    string? ReturnMessage,
    IReadOnlyList<PreboardingFileDto> Files,
    PreboardingBankDto? Bank);

/// <param name="Team"><c>IT</c> or <c>Admin</c>.</param>
/// <param name="Status"><c>Held</c> (waiting for the documents), <c>NotSetUp</c> (no address — nothing will
/// be sent), <c>Queued</c>, <c>Sent</c>, <c>Failed</c>, or <c>NotSent</c> (no address when it fired).</param>
public record PreboardingHandoffTeamDto(string Team, string? Address, string Status, DateTimeOffset? SentAt, string? Detail);

public record PreboardingDto(
    Guid OfferId,
    string Reference,
    Guid CandidateId,
    string CandidateName,
    string Position,
    string DepartmentName,
    DateOnly JoiningDate,
    DateTimeOffset? SignedAt,
    bool WindowOpen,
    IReadOnlyList<PreboardingDocumentDto> Documents,
    int RequiredDocuments,
    int RequiredApproved,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? RemindedAt,
    bool CanRemind,
    DateTimeOffset? HandoffAt,
    IReadOnlyList<PreboardingHandoffTeamDto> Handoff);

public record ReviewPreboardingDocumentRequest
{
    /// <summary>The document's revision as shown to the recruiter.</summary>
    [Required]
    public int? Revision { get; init; }
}

public record ReturnPreboardingDocumentRequest
{
    [Required]
    public int? Revision { get; init; }

    /// <summary>To the candidate, in the recruiter's words — emailed and shown on their link.</summary>
    [Required, StringLength(2000)]
    public string Message { get; init; } = string.Empty;
}

public record RevealedBankNumberDto(string AccountNumber);

/// <summary>A document's file, for <c>File(...)</c>.</summary>
public record PreboardingFileContent(Stream Content, string ContentType, string DownloadName);

// ── Settings ────────────────────────────────────────────────────────────────

public record PreboardingSettingsDto(
    bool NrcRequired,
    bool BankAccountRequired,
    bool EducationRequired,
    bool PhotoRequired,
    string? ItHandoffEmail,
    string? AdminHandoffEmail);

/// <summary>Every value, always: an omitted flag is a 400, not a silent "optional".</summary>
public record UpdatePreboardingSettingsRequest
{
    [Required] public bool? NrcRequired { get; init; }
    [Required] public bool? BankAccountRequired { get; init; }
    [Required] public bool? EducationRequired { get; init; }
    [Required] public bool? PhotoRequired { get; init; }

    [StringLength(320)]
    public string? ItHandoffEmail { get; init; }

    [StringLength(320)]
    public string? AdminHandoffEmail { get; init; }
}
