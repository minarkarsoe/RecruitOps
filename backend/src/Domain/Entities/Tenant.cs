using RecruitOps.Domain.Common;

namespace RecruitOps.Domain.Entities;

/// <summary>An agency using the platform. Root of tenant isolation (Module 1).</summary>
public class Tenant : BaseEntity
{
    // TODO: Name, Slug, PlanTier (Starter/Growth/Pro), BrandLogoUrl, IsActive ...
}
