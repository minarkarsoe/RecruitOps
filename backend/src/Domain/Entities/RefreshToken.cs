using System;
using RecruitOps.Domain.Common;

namespace RecruitOps.Domain.Entities;

/// <summary>
/// Server-side persisted refresh token for silent auth renewal and session revocation.
/// </summary>
public class RefreshToken : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>Cryptographically secure random string (base64/hex).</summary>
    public string Token { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>Populated during token rotation to trace replacement history.</summary>
    public string? ReplacedByToken { get; set; }

    public string? CreatedByIp { get; set; }
    public string? RevokedByIp { get; set; }

    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;
    public bool IsActive => !IsRevoked && !IsExpired;
}
