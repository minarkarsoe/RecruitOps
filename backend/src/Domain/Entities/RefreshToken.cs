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

    /// <summary>SHA-256 of the token, lowercase hex — never the token itself (ADR-0028). A copy of
    /// this table must not be a set of live sessions.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>When the sign-in this token descends from happened. Carried through every rotation;
    /// the session ends <c>Jwt:SessionMaxDays</c> after it however active it is (ADR-0028).</summary>
    public DateTimeOffset SessionStartedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>The hash of the token that replaced this one on rotation — the chain, traced
    /// without storing a usable token (ADR-0028).</summary>
    public string? ReplacedByTokenHash { get; set; }

    public string? CreatedByIp { get; set; }
    public string? RevokedByIp { get; set; }

    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;
    public bool IsActive => !IsRevoked && !IsExpired;
}
