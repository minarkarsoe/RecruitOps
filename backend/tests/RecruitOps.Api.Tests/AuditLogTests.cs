using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RecruitOps.Api.Auth;
using RecruitOps.Application.DTOs;
using RecruitOps.Domain.Entities;
using RecruitOps.Domain.Enums;
using RecruitOps.Infrastructure.Persistence;
using Xunit;

namespace RecruitOps.Api.Tests;

/// <summary>The audit log (ADR-0027): who changed access, and who signed in.
/// <para>Each test finds its own entries by target or actor — the class shares one database, so a
/// test that read "the latest entry" would read another test's.</para></summary>
public class AuditLogTests : IClassFixture<CustomWebAppFactory>
{
    private const string Password = "P@ssw0rd-Test-123";
    private const string RequisitionsRead = "permission:requisitions:requisitions:read";
    private const string RequisitionsApprove = "permission:requisitions:requisitions:approve";

    private readonly CustomWebAppFactory _factory;
    private readonly Module3Scenario _scenario;

    public AuditLogTests(CustomWebAppFactory factory)
    {
        _factory = factory;
        _scenario = new Module3Scenario(factory);
    }

    private HttpClient Admin() => _scenario.Client(Roles.Admin, _factory.AdminUserId);

    private async Task<List<AuditLogEntryDto>> EntriesAsync(string query)
    {
        var res = await Admin().GetAsync($"/api/audit-log?pageSize=100&{query}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<PagedResult<AuditLogEntryDto>>())!.Items.ToList();
    }

    private Task<List<AuditLogEntryDto>> AboutAsync(Guid targetId) => EntriesAsync($"targetId={targetId}");

    private async Task<RoleDetailDto> RoleAsync(string baseRole, params string[] permissions)
    {
        var res = await Admin().PostAsJsonAsync("/api/roles",
            new CreateRoleRequest($"Audited {Guid.NewGuid():N}", null, "test", permissions, baseRole));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<RoleDetailDto>())!;
    }

    private async Task<(UserDetailDto User, string Email)> UserAsync(string? role = "Recruiter", Guid? roleId = null)
    {
        var email = $"audited.{Guid.NewGuid():N}@alpha.test";
        var res = await Admin().PostAsJsonAsync("/api/users",
            new CreateUserRequest(email, "Audited Person", Password, roleId, roleId is null ? role : null));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return ((await res.Content.ReadFromJsonAsync<UserDetailDto>())!, email);
    }

    // ---------------------------------------------------------------- roles

    [Fact]
    public async Task Creating_A_Role_Is_Recorded_With_Who_Did_It()
    {
        var role = await RoleAsync("Approver", RequisitionsApprove);

        var entry = Assert.Single(await AboutAsync(role.Id));
        Assert.Equal(AuditAction.RoleCreated, entry.Action);
        Assert.Equal(_factory.AdminUserId, entry.ActorUserId);
        Assert.False(string.IsNullOrWhiteSpace(entry.ActorName));
        Assert.Equal("Role", entry.TargetType);
        Assert.Equal(role.Name, entry.TargetName);
        Assert.Contains(role.Name, entry.Summary);
        Assert.Contains("Approver", entry.Details!.Value.GetRawText());
        Assert.Contains(RequisitionsApprove, entry.Details!.Value.GetRawText());
        Assert.False(entry.IsRefusal);
    }

    [Fact]
    public async Task Changing_A_Role_Records_What_Moved_Including_Its_Holders()
    {
        var role = await RoleAsync("Recruiter", RequisitionsRead);
        var (holder, _) = await UserAsync(roleId: role.Id);

        var res = await Admin().PutAsJsonAsync($"/api/roles/{role.Id}",
            new UpdateRoleRequest(role.Name, null, true, new[] { RequisitionsApprove }, "Approver"));
        res.EnsureSuccessStatusCode();

        var entry = Assert.Single(await AboutAsync(role.Id), e => e.Action == AuditAction.RoleUpdated);
        var details = entry.Details!.Value.GetRawText();
        Assert.Contains("Recruiter", details);
        Assert.Contains("Approver", details);
        Assert.Contains(RequisitionsApprove, details); // added
        Assert.Contains(RequisitionsRead, details);    // removed
        Assert.Contains(holder.Id.ToString(), details); // whose access moved
    }

    [Fact]
    public async Task Saving_A_Role_Unchanged_Records_Nothing()
    {
        var role = await RoleAsync("Recruiter", RequisitionsRead);

        (await Admin().PutAsJsonAsync($"/api/roles/{role.Id}",
            new UpdateRoleRequest(role.Name, role.Description, true, new[] { RequisitionsRead }))).EnsureSuccessStatusCode();

        Assert.DoesNotContain(await AboutAsync(role.Id), e => e.Action == AuditAction.RoleUpdated);
    }

    [Fact]
    public async Task Deleting_A_Role_Is_Recorded_And_Its_History_Survives()
    {
        var role = await RoleAsync("Interviewer");

        (await Admin().DeleteAsync($"/api/roles/{role.Id}")).EnsureSuccessStatusCode();

        var entries = await AboutAsync(role.Id);
        Assert.Contains(entries, e => e.Action == AuditAction.RoleCreated);
        Assert.Contains(entries, e => e.Action == AuditAction.RoleDeleted && e.TargetName == role.Name);
    }

    [Fact]
    public async Task A_Rejected_Change_Leaves_No_Entry()
    {
        var name = $"Never {Guid.NewGuid():N}";
        var res = await Admin().PostAsJsonAsync("/api/roles",
            new CreateRoleRequest(name, null, null, new[] { RequisitionsRead }, "Admin"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);

        Assert.DoesNotContain(await EntriesAsync("category=Roles"), e => e.TargetName == name);
    }

    // ---------------------------------------------------------------- users

    [Fact]
    public async Task The_User_Directory_Is_Recorded_Create_Rename_Role_Deactivate_Reactivate()
    {
        var (user, email) = await UserAsync("Recruiter");

        (await Admin().PutAsJsonAsync($"/api/users/{user.Id}", new UpdateUserRequest("Renamed Person"))).EnsureSuccessStatusCode();
        (await Admin().PutAsJsonAsync($"/api/users/{user.Id}", new UpdateUserRequest("Renamed Person", null, "Approver"))).EnsureSuccessStatusCode();
        (await Admin().PutAsync($"/api/users/{user.Id}/deactivate", null)).EnsureSuccessStatusCode();
        (await Admin().PutAsync($"/api/users/{user.Id}/reactivate", null)).EnsureSuccessStatusCode();

        var entries = await AboutAsync(user.Id);
        var actions = entries.Select(e => e.Action).ToList();
        Assert.Contains(AuditAction.UserCreated, actions);
        Assert.Contains(AuditAction.UserUpdated, actions);
        Assert.Contains(AuditAction.UserDeactivated, actions);
        Assert.Contains(AuditAction.UserReactivated, actions);

        var roleChange = Assert.Single(entries, e => e.Action == AuditAction.UserRoleChanged);
        Assert.Contains("Recruiter", roleChange.Summary);
        Assert.Contains("Approver", roleChange.Summary);

        var created = Assert.Single(entries, e => e.Action == AuditAction.UserCreated);
        Assert.Contains(email, created.Details!.Value.GetRawText());

        // Newest first.
        Assert.Equal(AuditAction.UserReactivated, entries[0].Action);
    }

    [Fact]
    public async Task No_Entry_Ever_Holds_A_Password()
    {
        var (user, _) = await UserAsync("Recruiter");
        foreach (var entry in await AboutAsync(user.Id))
        {
            Assert.DoesNotContain(Password, entry.Summary);
            Assert.DoesNotContain(Password, entry.Details?.GetRawText() ?? string.Empty);
        }
    }

    // ---------------------------------------------------------------- sign-ins

    private Task<HttpResponseMessage> SignInAsync(string email, string password) =>
        _factory.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = password });

    [Fact]
    public async Task Sign_Ins_Are_Recorded_Successes_And_Failures()
    {
        var (user, email) = await UserAsync("Recruiter");

        Assert.Equal(HttpStatusCode.OK, (await SignInAsync(email, Password)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SignInAsync(email, "wrong-password-123")).StatusCode);

        var entries = await AboutAsync(user.Id);
        var ok = Assert.Single(entries, e => e.Action == AuditAction.SignInSucceeded);
        Assert.Equal(user.Id, ok.ActorUserId);
        var failed = Assert.Single(entries, e => e.Action == AuditAction.SignInFailed);
        Assert.True(failed.IsRefusal);
        Assert.Contains("password", failed.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("wrong-password-123", failed.Details?.GetRawText() ?? string.Empty);
    }

    [Fact]
    public async Task A_Sign_In_For_No_Account_Is_Recorded_With_The_Email_Tried()
    {
        var email = $"nobody.{Guid.NewGuid():N}@alpha.test";
        Assert.Equal(HttpStatusCode.Unauthorized, (await SignInAsync(email, Password)).StatusCode);

        var entry = Assert.Single(await EntriesAsync("category=Refused"), e => e.ActorName == email);
        Assert.Equal(AuditAction.SignInFailed, entry.Action);
        Assert.Null(entry.ActorUserId);
    }

    // ---------------------------------------------------------------- refusals

    /// <summary>A Recruiter-based delegate who may manage users and roles, and read requisitions.</summary>
    private async Task<(Guid Id, HttpClient Client)> DelegateAsync()
    {
        var role = await RoleAsync("Recruiter",
            "permission:users:users:read", "permission:users:users:create", "permission:users:users:update",
            "permission:roles:roles:read", "permission:roles:roles:create", RequisitionsRead);
        var (lead, _) = await UserAsync(roleId: role.Id);
        return (lead.Id, _scenario.Client(Roles.Recruiter, lead.Id));
    }

    [Fact]
    public async Task A_Refused_Access_Change_Is_Recorded_And_The_Change_Is_Not()
    {
        var (leadId, lead) = await DelegateAsync();
        var (target, _) = await UserAsync("Interviewer");
        var tooMuch = await RoleAsync("Interviewer", RequisitionsApprove); // the lead lacks approve

        // The service has already moved the target onto the new role in memory when the ceiling
        // refuses. Recording the refusal must not save that.
        var res = await lead.PutAsJsonAsync($"/api/users/{target.Id}", new UpdateUserRequest("Audited Person", tooMuch.Id));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);

        var after = await Admin().GetFromJsonAsync<UserDetailDto>($"/api/users/{target.Id}");
        Assert.NotEqual(tooMuch.Id, after!.RoleId);
        Assert.Equal("Interviewer", after.Role);

        var refusal = Assert.Single(await EntriesAsync($"actorUserId={leadId}&action=AccessChangeRefused"));
        Assert.True(refusal.IsRefusal);
        Assert.Contains(RequisitionsApprove, refusal.Summary + refusal.Details?.GetRawText());
        Assert.Contains(target.Id.ToString(), refusal.Details!.Value.GetRawText()); // the endpoint tried
    }

    // ---------------------------------------------------------------- reading

    [Theory]
    [InlineData(Roles.Recruiter)]
    [InlineData(Roles.HiringManager)]
    public async Task Only_Holders_Of_System_Audit_Read_The_Log(string role)
    {
        var userId = role == Roles.Recruiter ? _factory.RecruiterUserId : _factory.HiringManagerUserId;
        var res = await _scenario.Client(role, userId).GetAsync("/api/audit-log");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Categories_Filter_The_Log()
    {
        var role = await RoleAsync("Interviewer");
        Assert.Contains(await EntriesAsync("category=Roles"), e => e.TargetId == role.Id);
        Assert.DoesNotContain(await EntriesAsync("category=SignIns"), e => e.TargetId == role.Id);
        Assert.All(await EntriesAsync("category=Refused"), e => Assert.True(e.IsRefusal));
    }

    [Fact]
    public async Task Another_Tenants_Entries_Are_Not_Shown()
    {
        var foreign = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.AuditLogEntries.Add(new AuditLogEntry
            {
                TenantId = _factory.TenantB,
                Action = AuditAction.RoleCreated,
                TargetType = "Role",
                TargetId = foreign,
                TargetName = "Tenant B role",
                Summary = "Created role in B",
            });
            await db.SaveChangesAsync();
        }

        Assert.Empty(await AboutAsync(foreign));
    }

    [Fact]
    public async Task Entries_Cannot_Be_Edited_Or_Deleted()
    {
        var role = await RoleAsync("Interviewer");
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entry = await db.AuditLogEntries.IgnoreQueryFilters().FirstAsync(e => e.TargetId == role.Id);

        entry.Summary = "Nothing happened here";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());

        db.ChangeTracker.Clear();
        db.AuditLogEntries.Remove(await db.AuditLogEntries.IgnoreQueryFilters().FirstAsync(e => e.TargetId == role.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }
}
