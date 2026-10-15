using RecruitOps.Domain.Enums;

namespace RecruitOps.Domain.Entities;

/// <summary>One line of the audit log (ADR-0027): who changed access, or who signed in.
///
/// <para><b>Append-only.</b> Not a <c>BaseEntity</c> — there is no <c>UpdatedAt</c> because there
/// is no update. <c>AppDbContext</c> throws on a modified or deleted entry, and the database's
/// triggers refuse <c>UPDATE</c>/<c>DELETE</c>/<c>TRUNCATE</c>.</para>
///
/// <para><b>No foreign keys, and names are snapshots.</b> A deleted role or user keeps its entries,
/// and an entry says what the actor and target were called <i>then</i> — the point of the log is
/// what happened, not what things are called now.</para>
///
/// <para><b>Not <c>ITenantScoped</c>:</b> a failed sign-in for an email that matches no account has
/// no tenant (null). The read side filters by tenant explicitly (ADR-0027 §5).</para>
/// </summary>
public class AuditLogEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;

    public Guid? TenantId { get; set; }

    public AuditAction Action { get; set; }

    /// <summary>Null for an anonymous attempt (a failed sign-in) or the system itself.</summary>
    public Guid? ActorUserId { get; set; }

    /// <summary>The actor's display name at the time — or the email a failed sign-in tried.</summary>
    public string? ActorName { get; set; }

    /// <summary><c>"User"</c> or <c>"Role"</c> today.</summary>
    public string? TargetType { get; set; }

    public Guid? TargetId { get; set; }

    public string? TargetName { get; set; }

    /// <summary>One sentence for a person: "Changed Jane Doe's role from Recruiter to Approver."</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>Before/after, as JSON. Never secrets or business content.</summary>
    public string? DetailsJson { get; set; }

    public string? IpAddress { get; set; }

    /// <summary>The id problem-details error responses show, so a support ticket finds its entry.</summary>
    public string? TraceId { get; set; }
}
