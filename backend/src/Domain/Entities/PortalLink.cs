using RecruitOps.Domain.Common;

namespace RecruitOps.Domain.Entities;

/// <summary>A no-login shareable link exposing a shortlist to a client (Module 2).</summary>
public class PortalLink : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid JobId { get; set; }
    // TODO: Token (unguessable), ExpiresAt, IsRevoked, ApplicationIds (shortlist) ...
}
