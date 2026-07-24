namespace RecruitOps.Domain.Common;

/// <summary>Marks an entity that belongs to a single tenant (agency).
/// Infrastructure applies a global query filter on TenantId for data isolation
/// (Module 1: Multi-Tenant Architecture).</summary>
public interface ITenantScoped
{
    Guid TenantId { get; set; }
}
