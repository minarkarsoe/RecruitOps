using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RecruitOps.Api.Auth;
using RecruitOps.Application.DTOs;
using RecruitOps.Domain.Entities;
using RecruitOps.Domain.Enums;
using RecruitOps.Infrastructure.Persistence;
using Xunit;

namespace RecruitOps.Api.Tests;

/// <summary>Role changes that must not happen through the user directory, whoever asks
/// (security review, 2026-10-15). A guard fires only on an <i>actual</i> role change: the edit
/// dialog sends the current role along with a new display name, and that must keep working.</summary>
public class UserDirectoryGuardsTests : IClassFixture<CustomWebAppFactory>
{
    private const string Password = "P@ssw0rd-Test-123";

    private readonly CustomWebAppFactory _factory;
    private readonly Module3Scenario _scenario;

    public UserDirectoryGuardsTests(CustomWebAppFactory factory)
    {
        _factory = factory;
        _scenario = new Module3Scenario(factory);
    }

    private HttpClient AdminAs(Guid userId) => _scenario.Client(Roles.Admin, userId);

    private async Task<UserDetailDto> CreateUserAsync(string role)
    {
        var res = await AdminAs(_factory.AdminUserId).PostAsJsonAsync("/api/users",
            new CreateUserRequest($"guard.{Guid.NewGuid():N}@alpha.test", "Guarded", Password, null, role));
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<UserDetailDto>())!;
    }

    [Fact]
    public async Task An_Admin_Cannot_Change_Their_Own_Role()
    {
        var self = await CreateUserAsync("Admin");

        var res = await AdminAs(self.Id).PutAsJsonAsync($"/api/users/{self.Id}",
            new UpdateUserRequest("Guarded", null, "Recruiter"));

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("Admin", (await AdminAs(_factory.AdminUserId).GetFromJsonAsync<UserDetailDto>($"/api/users/{self.Id}"))!.Role);
    }

    [Fact]
    public async Task An_Admin_Can_Rename_Themselves_While_The_Dialog_Resends_Their_Role()
    {
        var self = await CreateUserAsync("Admin");

        var res = await AdminAs(self.Id).PutAsJsonAsync($"/api/users/{self.Id}",
            new UpdateUserRequest("Renamed Admin", self.RoleId, null));

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("Renamed Admin", (await res.Content.ReadFromJsonAsync<UserDetailDto>())!.DisplayName);
    }

    [Fact]
    public async Task A_SuperAdmin_Accounts_Role_Is_Not_Managed_Through_The_Directory()
    {
        var id = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.Add(new User
            {
                Id = id, TenantId = _factory.TenantA, Email = $"super.{id:N}@alpha.test", DisplayName = "Vendor",
                Role = UserRole.SuperAdmin, IsSuperAdmin = true, IsActive = true, PasswordHash = "x",
            });
            await db.SaveChangesAsync();
        }

        var res = await AdminAs(_factory.AdminUserId).PutAsJsonAsync($"/api/users/{id}",
            new UpdateUserRequest("Vendor", null, "Recruiter"));

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task Another_Admin_Can_Change_An_Admins_Role()
    {
        var target = await CreateUserAsync("Admin");

        var res = await AdminAs(_factory.AdminUserId).PutAsJsonAsync($"/api/users/{target.Id}",
            new UpdateUserRequest("Guarded", null, "HrDirector"));

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("HrDirector", (await res.Content.ReadFromJsonAsync<UserDetailDto>())!.Role);
    }
}

/// <summary>Kept apart so its database holds a known number of Admins: the factory's one.</summary>
public class LastAdminGuardTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;
    private readonly Module3Scenario _scenario;

    public LastAdminGuardTests(CustomWebAppFactory factory)
    {
        _factory = factory;
        _scenario = new Module3Scenario(factory);
    }

    [Fact]
    public async Task The_Last_Active_Admin_Cannot_Be_Demoted_Until_There_Is_Another()
    {
        // A caller that is not the target, so the self-change guard is not what answers — and not
        // a database Admin either, or there would be two. A SuperAdmin claim passes the permission
        // check (the handler's bypass) without being a row the floor counts.
        var caller = _scenario.Client(Roles.Admin, Guid.NewGuid());
        caller.DefaultRequestHeaders.Add("X-Test-IsSuperAdmin", "true");
        var demote = new UpdateUserRequest("Alpha Admin", null, "Recruiter");

        var refused = await caller.PutAsJsonAsync($"/api/users/{_factory.AdminUserId}", demote);
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);

        (await caller.PostAsJsonAsync("/api/users", new CreateUserRequest(
            $"second.{Guid.NewGuid():N}@alpha.test", "Second Admin", "P@ssw0rd-Test-123", null, "Admin")))
            .EnsureSuccessStatusCode();

        var allowed = await caller.PutAsJsonAsync($"/api/users/{_factory.AdminUserId}", demote);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }
}
