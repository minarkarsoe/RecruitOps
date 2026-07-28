using RecruitOps.Domain.Common;

namespace RecruitOps.Domain.Entities;

/// <summary>The customer company. One row per deployment — each company gets its own
/// database (ADR-0004). Holds branding/settings used by job pages and emails.</summary>
public class Company : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Subdomain slug used for routing (ADR-0004).</summary>
    public string Slug { get; set; } = string.Empty;

    public string? LogoUrl { get; set; }
    public bool IsActive { get; set; } = true;
}
