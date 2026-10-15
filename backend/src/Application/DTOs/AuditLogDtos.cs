using System.Text.Json;
using RecruitOps.Domain.Enums;

namespace RecruitOps.Application.DTOs;

/// <summary>One row of the audit log viewer (ADR-0027).</summary>
/// <param name="ActionLabel">What a person reads — resolved server-side, so the log and its filter
/// cannot disagree about what an action is called.</param>
/// <param name="IsRefusal">Someone was turned away: a failed sign-in or a blocked access change.
/// The one thing the viewer colours red.</param>
/// <param name="Details">The before/after, as stored. Never secrets.</param>
public sealed record AuditLogEntryDto(
    Guid Id,
    DateTimeOffset OccurredAt,
    AuditAction Action,
    string ActionLabel,
    bool IsRefusal,
    Guid? ActorUserId,
    string? ActorName,
    string? TargetType,
    Guid? TargetId,
    string? TargetName,
    string Summary,
    JsonElement? Details,
    string? IpAddress,
    string? TraceId);

/// <summary>The viewer's filter group. Server-side so it means one thing.</summary>
public enum AuditCategory
{
    SignIns,
    Users,
    Roles,
    /// <summary>Failed sign-ins and refused access changes — the rows worth a second look.</summary>
    Refused,
}

public sealed class AuditLogQuery
{
    public AuditCategory? Category { get; set; }

    public AuditAction? Action { get; set; }

    public Guid? ActorUserId { get; set; }

    public string? TargetType { get; set; }

    public Guid? TargetId { get; set; }

    public DateTimeOffset? From { get; set; }

    public DateTimeOffset? To { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 25;
}
