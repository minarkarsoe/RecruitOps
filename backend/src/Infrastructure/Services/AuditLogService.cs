using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RecruitOps.Application.Common;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;
using RecruitOps.Domain.Enums;
using RecruitOps.Infrastructure.Persistence;

namespace RecruitOps.Infrastructure.Services;

/// <summary>ADR-0027's read side. Who may call it is decided at the controller
/// (<c>permission:system:system:audit</c>); this decides which entries a tenant sees.</summary>
public sealed class AuditLogService : IAuditLogService
{
    private const int MaxPageSize = 100;

    private static readonly AuditAction[] SignIns = { AuditAction.SignInSucceeded, AuditAction.SignInFailed };

    private static readonly AuditAction[] UserActions =
    {
        AuditAction.UserCreated, AuditAction.UserUpdated, AuditAction.UserRoleChanged,
        AuditAction.UserDeactivated, AuditAction.UserReactivated,
    };

    private static readonly AuditAction[] RoleActions =
        { AuditAction.RoleCreated, AuditAction.RoleUpdated, AuditAction.RoleDeleted };

    /// <summary>Someone was turned away. The one thing the viewer colours red.</summary>
    private static readonly AuditAction[] Refusals = { AuditAction.SignInFailed, AuditAction.AccessChangeRefused };

    private readonly AppDbContext _db;
    private readonly ICurrentTenant _tenant;

    public AuditLogService(AppDbContext db, ICurrentTenant tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<PagedResult<AuditLogEntryDto>> QueryAsync(AuditLogQuery query, CancellationToken ct = default)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);
        var tenant = _tenant.TenantId;

        // The entity is not ITenantScoped, so this filter is the tenant boundary — written here,
        // once. A null tenant is a failed sign-in for an email that matches no account: there is
        // no tenant to give it, and on a one-company install (ADR-0004) it belongs to its auditors.
        var entries = _db.AuditLogEntries.AsNoTracking()
            .Where(e => e.TenantId == tenant || e.TenantId == null);

        if (query.Category is { } category)
        {
            var actions = category switch
            {
                AuditCategory.SignIns => SignIns,
                AuditCategory.Users => UserActions,
                AuditCategory.Roles => RoleActions,
                AuditCategory.Refused => Refusals,
                _ => Array.Empty<AuditAction>(),
            };
            entries = entries.Where(e => actions.Contains(e.Action));
        }

        if (query.Action is { } action) entries = entries.Where(e => e.Action == action);
        if (query.ActorUserId is { } actor) entries = entries.Where(e => e.ActorUserId == actor);
        if (!string.IsNullOrWhiteSpace(query.TargetType)) entries = entries.Where(e => e.TargetType == query.TargetType);
        if (query.TargetId is { } target) entries = entries.Where(e => e.TargetId == target);
        if (query.From is { } from) entries = entries.Where(e => e.OccurredAt >= from);
        if (query.To is { } to) entries = entries.Where(e => e.OccurredAt < to);

        var totalCount = await entries.CountAsync(ct);

        var rows = await entries
            .OrderByDescending(e => e.OccurredAt)
            .ThenByDescending(e => e.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = rows.Select(e => new AuditLogEntryDto(
            e.Id,
            e.OccurredAt,
            e.Action,
            LabelFor(e.Action),
            Refusals.Contains(e.Action),
            e.ActorUserId,
            e.ActorName,
            e.TargetType,
            e.TargetId,
            e.TargetName,
            e.Summary,
            ParseDetails(e.DetailsJson),
            e.IpAddress,
            e.TraceId)).ToList();

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        return new PagedResult<AuditLogEntryDto>(items, page, pageSize, totalCount, totalPages);
    }

    private static JsonElement? ParseDetails(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private static string LabelFor(AuditAction action) => action switch
    {
        AuditAction.SignInSucceeded => "Signed in",
        AuditAction.SignInFailed => "Sign-in failed",
        AuditAction.UserCreated => "User created",
        AuditAction.UserUpdated => "User renamed",
        AuditAction.UserRoleChanged => "User's role changed",
        AuditAction.UserDeactivated => "User deactivated",
        AuditAction.UserReactivated => "User reactivated",
        AuditAction.RoleCreated => "Role created",
        AuditAction.RoleUpdated => "Role changed",
        AuditAction.RoleDeleted => "Role deleted",
        AuditAction.AccessChangeRefused => "Access change refused",
        _ => action.ToString(),
    };
}
