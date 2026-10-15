using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RecruitOps.Application.Common;
using RecruitOps.Application.Interfaces;
using RecruitOps.Domain.Entities;
using RecruitOps.Domain.Enums;
using RecruitOps.Infrastructure.Persistence;

namespace RecruitOps.Infrastructure.Services;

/// <summary>ADR-0027's write side. See <see cref="IAuditTrail"/> for which method to call when.</summary>
public sealed class AuditTrail : IAuditTrail
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly AppDbContext _db;
    private readonly ICurrentUser _user;
    private readonly ICurrentTenant _tenant;
    private readonly IRequestOrigin _origin;
    private readonly IServiceScopeFactory _scopes;

    public AuditTrail(
        AppDbContext db, ICurrentUser user, ICurrentTenant tenant, IRequestOrigin origin, IServiceScopeFactory scopes)
    {
        _db = db;
        _user = user;
        _tenant = tenant;
        _origin = origin;
        _scopes = scopes;
    }

    public async Task RecordAsync(
        AuditAction action, AuditTarget? target, string summary, object? details = null,
        AuditActor? actor = null, Guid? tenantId = null, CancellationToken ct = default)
    {
        _db.AuditLogEntries.Add(await BuildAsync(action, target, summary, details, actor, tenantId, ct));
    }

    public async Task RecordStandaloneAsync(
        AuditAction action, AuditTarget? target, string summary, object? details = null,
        AuditActor? actor = null, Guid? tenantId = null, CancellationToken ct = default)
    {
        var entry = await BuildAsync(action, target, summary, details, actor, tenantId, ct);

        // A context of its own, never the request's: when this is a refusal, the request's context
        // may already hold the refused change, and saving it here would commit exactly that.
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.AuditLogEntries.Add(entry);
        await db.SaveChangesAsync(ct);
    }

    private async Task<AuditLogEntry> BuildAsync(
        AuditAction action, AuditTarget? target, string summary, object? details,
        AuditActor? actor, Guid? tenantId, CancellationToken ct)
    {
        actor ??= await SignedInActorAsync(ct);
        var tenant = tenantId ?? (_tenant.TenantId == Guid.Empty ? null : _tenant.TenantId);

        return new AuditLogEntry
        {
            TenantId = tenant,
            Action = action,
            ActorUserId = actor?.UserId,
            ActorName = Clip(actor?.Name, 256),
            TargetType = target?.Type,
            TargetId = target?.Id,
            TargetName = Clip(target?.Name, 256),
            Summary = Clip(summary, 1000) ?? string.Empty,
            DetailsJson = details is null ? null : JsonSerializer.Serialize(details, Json),
            IpAddress = Clip(_origin.IpAddress, 64),
            TraceId = Clip(_origin.TraceId, 128),
        };
    }

    /// <summary>The signed-in user, named as they are called now — which is the snapshot.</summary>
    private async Task<AuditActor?> SignedInActorAsync(CancellationToken ct)
    {
        if (_user.UserId is not { } id) return null;

        var name = await _db.Users.AsNoTracking().IgnoreQueryFilters()
            .Where(u => u.Id == id)
            .Select(u => u.DisplayName)
            .FirstOrDefaultAsync(ct);

        return new AuditActor(id, name);
    }

    private static string? Clip(string? value, int max) =>
        value is null || value.Length <= max ? value : value[..max];
}
