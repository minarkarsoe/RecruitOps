using RecruitOps.Domain.Enums;

namespace RecruitOps.Application.Interfaces;

/// <summary>The user or role an entry is about. Names are snapshots (ADR-0027).</summary>
public sealed record AuditTarget(string Type, Guid? Id, string? Name)
{
    public static AuditTarget User(Guid id, string name) => new("User", id, name);
    public static AuditTarget Role(Guid id, string name) => new("Role", id, name);
    public static AuditTarget Department(Guid id, string name) => new("Department", id, name);
    /// <summary>A candidate is recorded by id alone. The log is permanent and cannot be erased, a
    /// candidate's name is their own data rather than a colleague's, and not every reader of the log
    /// may see candidate data (Module 2.7 security review, M2).</summary>
    public static AuditTarget Candidate(Guid id) => new("Candidate", id, null);
}

/// <summary>Who acted, when it is not the signed-in user — the person signing in, or the system.</summary>
public sealed record AuditActor(Guid? UserId, string? Name);

/// <summary>Writes the audit log (ADR-0027). Two ways in, and choosing wrongly is a bug:
/// <list type="bullet">
/// <item><see cref="RecordAsync"/> <b>adds</b> an entry to the current request's unit of work. It
/// is saved by the caller's own <c>SaveChanges</c>, so the change and its entry commit together.
/// Call it before that <c>SaveChanges</c>.</item>
/// <item><see cref="RecordStandaloneAsync"/> <b>saves</b> an entry through a separate
/// <c>DbContext</c>, for an event whose request saves nothing — a failed sign-in, a refusal. Never
/// use the request's context for those: it may hold the very change that was just refused.</item>
/// </list>
/// The actor defaults to the signed-in user; the tenant to the current one.</summary>
public interface IAuditTrail
{
    Task RecordAsync(
        AuditAction action, AuditTarget? target, string summary, object? details = null,
        AuditActor? actor = null, Guid? tenantId = null, CancellationToken ct = default);

    Task RecordStandaloneAsync(
        AuditAction action, AuditTarget? target, string summary, object? details = null,
        AuditActor? actor = null, Guid? tenantId = null, CancellationToken ct = default);
}
