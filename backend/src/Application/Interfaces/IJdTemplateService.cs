using RecruitOps.Application.DTOs;

namespace RecruitOps.Application.Interfaces;

/// <summary>Module 1.2 — reusable Job Description templates.</summary>
public interface IJdTemplateService
{
    /// <summary>Active templates usable by the current user: company-wide ones, plus
    /// those belonging to a department they can access (ADR-0003).</summary>
    Task<IReadOnlyList<JdTemplateDto>> GetTemplatesAsync(CancellationToken ct = default);

    /// <summary>Null if the referenced department does not exist or is not accessible.</summary>
    Task<JdTemplateDto?> CreateAsync(CreateJdTemplateRequest request, CancellationToken ct = default);
}
