using RecruitOps.Application.DTOs;
using RecruitOps.Domain;

namespace RecruitOps.Application.Interfaces;

/// <summary>Module 5.2's Pipeline &amp; source page (ADR-0032). Null for a caller with no access to it — a
/// Hiring Manager, an approver, an interviewer.</summary>
public interface IPipelineAnalyticsService
{
    Task<PipelineAnalyticsDto?> GetAsync(
        PipelineAnalytics.Window window, Guid? postingId, PipelineAnalytics.ChannelGroup? source, CancellationToken ct = default);
}
