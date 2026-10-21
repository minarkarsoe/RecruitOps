using RecruitOps.Application.DTOs;

namespace RecruitOps.Application.Interfaces;

/// <summary>Module 4 offers (ADR-0030). Reach is decided per offer: recruitment staff see and manage
/// every offer; a Hiring Manager sees their departments' offers, read-only and without salary unless
/// the company shows it; anyone named on an offer's approval step sees that offer and decides it.
/// An offer outside the caller's reach is null (404), never a refusal that confirms it exists.
/// <see cref="InvalidOperationException"/> is a business refusal (409).</summary>
public interface IOfferService
{
    /// <param name="group"><c>awaiting</c> (pending approval), <c>with-candidate</c> (sent or viewed),
    /// <c>closed</c> (accepted, rejected, expired), or null for all.</param>
    Task<IReadOnlyList<OfferListItemDto>> ListAsync(
        string? group, Guid? departmentId, DateOnly? from, DateOnly? to, CancellationToken ct = default);

    Task<IReadOnlyList<OfferEligibleApplicationDto>> EligibleApplicationsAsync(CancellationToken ct = default);

    Task<OfferDetailDto?> GetAsync(Guid id, CancellationToken ct = default);

    Task<OfferLetterDto?> RenderLetterAsync(Guid id, CancellationToken ct = default);

    Task<OfferDetailDto?> CreateAsync(CreateOfferRequest request, CancellationToken ct = default);

    Task<OfferDetailDto?> UpdateAsync(Guid id, UpdateOfferRequest request, CancellationToken ct = default);

    Task<OfferDetailDto?> SubmitAsync(Guid id, CancellationToken ct = default);

    Task<OfferDetailDto?> DecideAsync(Guid id, ApprovalDecisionRequest request, CancellationToken ct = default);

    Task<OfferDetailDto?> ReviseAsync(Guid id, CancellationToken ct = default);

    /// <returns>False when not found or out of reach.</returns>
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<OfferTemplateDto>> TemplatesAsync(CancellationToken ct = default);

    Task<OfferTemplateDto> CreateTemplateAsync(SaveOfferTemplateRequest request, CancellationToken ct = default);

    Task<OfferTemplateDto?> UpdateTemplateAsync(Guid id, SaveOfferTemplateRequest request, CancellationToken ct = default);

    Task<OfferSettingsDto> SettingsAsync(CancellationToken ct = default);

    Task<OfferSettingsDto> UpdateSettingsAsync(UpdateOfferSettingsRequest request, CancellationToken ct = default);
}
