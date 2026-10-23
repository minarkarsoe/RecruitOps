using RecruitOps.Application.DTOs;

namespace RecruitOps.Application.Interfaces;

/// <summary>The recruiter's side of pre-boarding (Module 4.3, ADR-0030 §5b): review, reminders and the
/// handoff. <b>Recruitment staff only</b> — every method returns null for anyone else, as for an offer
/// that does not exist or was never accepted, and the controller turns that into a 404. A refusal a
/// recruiter can act on is an <see cref="InvalidOperationException"/>.</summary>
public interface IPreboardingService
{
    Task<PreboardingDto?> GetAsync(Guid offerId, CancellationToken ct = default);

    /// <summary>The file, after writing the audit entry that says who opened it.</summary>
    Task<PreboardingFileContent?> OpenFileAsync(Guid offerId, Guid fileId, CancellationToken ct = default);

    /// <summary>The whole account number, after writing the audit entry that says who read it.</summary>
    Task<RevealedBankNumberDto?> RevealBankNumberAsync(Guid offerId, CancellationToken ct = default);

    Task<PreboardingDto?> ApproveAsync(Guid offerId, string kind, int revision, CancellationToken ct = default);

    Task<PreboardingDto?> ReturnAsync(Guid offerId, string kind, int revision, string message, CancellationToken ct = default);

    Task<PreboardingDto?> RemindAsync(Guid offerId, CancellationToken ct = default);

    Task<PreboardingSettingsDto> SettingsAsync(CancellationToken ct = default);

    Task<PreboardingSettingsDto> UpdateSettingsAsync(UpdatePreboardingSettingsRequest request, CancellationToken ct = default);
}
