using RecruitOps.Application.DTOs;

namespace RecruitOps.Application.Interfaces;

/// <summary>Module 3.3 configuration — the criteria sets interviews are scored against.</summary>
public interface IScorecardTemplateService
{
    Task<IReadOnlyList<ScorecardTemplateDto>> ListAsync(CancellationToken ct = default);

    Task<ScorecardTemplateDto?> GetAsync(Guid id, CancellationToken ct = default);

    Task<ScorecardTemplateDto> CreateAsync(
        SaveScorecardTemplateRequest request, CancellationToken ct = default);

    Task<ScorecardTemplateDto?> UpdateAsync(
        Guid id, SaveScorecardTemplateRequest request, CancellationToken ct = default);

    /// <summary>Which template a posting's interviews are scored against right now:
    /// posting override → the posting's department → company-wide default (ADR-0017 §1).
    /// Null when the company has configured none.</summary>
    Task<ScorecardTemplateDto?> ResolveForPostingAsync(
        Guid jobPostingId, CancellationToken ct = default);
}
