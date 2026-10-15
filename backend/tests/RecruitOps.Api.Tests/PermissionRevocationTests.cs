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

/// <summary>A change to a role's permissions takes effect on the holder's next request.
/// <para>Until 2026-10-15 it did not: <c>InvalidateRolePermissionsCache</c> only logged, and the
/// per-user permission cache used a <i>sliding</i> 10-minute expiry — so a user who kept working
/// kept a revoked permission until the process restarted, and a deactivated role kept granting
/// its matrix because the evaluator never read <c>Role.IsActive</c>. The only earlier test of
/// this path ran against a stub evaluator whose invalidation methods were empty.</para>
/// <para>The holder is written straight to the database, in the shape
/// <c>UserService.AssignRoleToUserAsync</c> gives a custom-role holder (ADR-0021).</para></summary>
public class PermissionRevocationTests : IClassFixture<CustomWebAppFactory>
{
    /// <summary>Guards <c>GET /api/permissions</c> and nothing about it comes from a role policy, so
    /// the response reflects the permission matrix alone. (Most endpoints are role-policy gated;
    /// only the RBAC endpoints are permission-gated — see ADR-0021.)</summary>
    private const string RolesRead = "permission:roles:roles:read";
    private const string Probe = "/api/permissions";
    private const string PostingsRead = "permission:postings:postings:read";

    private readonly CustomWebAppFactory _factory;
    private readonly Module3Scenario _scenario;

    public PermissionRevocationTests(CustomWebAppFactory factory)
    {
        _factory = factory;
        _scenario = new Module3Scenario(factory);
    }

    private HttpClient Admin() => _scenario.Client(Roles.Admin, _factory.AdminUserId);

    /// <summary>A custom role holding <paramref name="permissions"/>, and a Hiring Manager who
    /// holds it. Hiring Manager's own seed matrix lacks <see cref="RolesRead"/>, so any access to
    /// the probe endpoint comes from the custom role alone.</summary>
    private Task<(RoleDetailDto Role, HttpClient Holder)> RoleHolderAsync(params string[] permissions) =>
        RoleHolderAsync(UserRole.HiringManager, permissions);

    private async Task<(RoleDetailDto Role, HttpClient Holder)> RoleHolderAsync(
        UserRole holderEnum, params string[] permissions)
    {
        var res = await Admin().PostAsJsonAsync("/api/roles",
            new CreateRoleRequest($"Permission Reader {Guid.NewGuid():N}", null, "test", permissions));
        res.EnsureSuccessStatusCode();
        var role = (await res.Content.ReadFromJsonAsync<RoleDetailDto>())!;

        var userId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.Add(new User
            {
                Id = userId,
                TenantId = _factory.TenantA,
                Email = $"holder.{userId:N}@alpha.test",
                DisplayName = "Role Holder",
                Role = holderEnum,
                RoleId = role.Id,
                IsActive = true,
                PasswordHash = "not-used",
            });
            await db.SaveChangesAsync();
        }

        return (role, _scenario.Client(holderEnum.ToString(), userId));
    }

    private Task<HttpResponseMessage> UpdateRole(RoleDetailDto role, bool isActive, params string[] permissions) =>
        Admin().PutAsJsonAsync($"/api/roles/{role.Id}",
            new UpdateRoleRequest(role.Name, role.Description, isActive, permissions));

    [Fact]
    public async Task A_Revoked_Permission_Stops_Working_On_The_Next_Request()
    {
        var (role, holder) = await RoleHolderAsync(RolesRead);

        // Granted, and now cached.
        Assert.Equal(HttpStatusCode.OK, (await holder.GetAsync(Probe)).StatusCode);

        (await UpdateRole(role, isActive: true, PostingsRead)).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Forbidden, (await holder.GetAsync(Probe)).StatusCode);
    }

    [Fact]
    public async Task A_Granted_Permission_Starts_Working_On_The_Next_Request()
    {
        var (role, holder) = await RoleHolderAsync(PostingsRead);

        // Refused, and the refusal's permission set is now cached.
        Assert.Equal(HttpStatusCode.Forbidden, (await holder.GetAsync(Probe)).StatusCode);

        (await UpdateRole(role, isActive: true, PostingsRead, RolesRead)).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.OK, (await holder.GetAsync(Probe)).StatusCode);
    }

    [Fact]
    public async Task A_Deactivated_Role_Grants_Nothing()
    {
        var (role, holder) = await RoleHolderAsync(RolesRead);
        Assert.Equal(HttpStatusCode.OK, (await holder.GetAsync(Probe)).StatusCode);

        (await UpdateRole(role, isActive: false, RolesRead)).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Forbidden, (await holder.GetAsync(Probe)).StatusCode);
    }

    // Security review of the develop merge, A1/B1. A custom-role holder carries the Recruiter enum
    // (that is what AssignRoleToUserAsync gives them), and the evaluator fell back to the enum's
    // seed matrix whenever the role yielded no rows. So deactivating a narrow custom role — or
    // emptying it — handed its holders everything a Recruiter has: requisitions, the user
    // directory. Deactivation is meant to withhold, and it granted.
    private const string RequisitionsProbe = "/api/requisitions"; // Recruiter's seed grants read

    [Fact]
    public async Task A_Deactivated_Custom_Role_Does_Not_Fall_Back_To_Its_Holders_Recruiter_Enum()
    {
        var (role, holder) = await RoleHolderAsync(UserRole.Recruiter, RolesRead);
        Assert.Equal(HttpStatusCode.Forbidden, (await holder.GetAsync(RequisitionsProbe)).StatusCode);

        (await UpdateRole(role, isActive: false, RolesRead)).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Forbidden, (await holder.GetAsync(Probe)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await holder.GetAsync(RequisitionsProbe)).StatusCode);
    }

    [Fact]
    public async Task A_Custom_Role_Emptied_Of_Permissions_Grants_Nothing()
    {
        var (role, holder) = await RoleHolderAsync(UserRole.Recruiter, RolesRead);
        Assert.Equal(HttpStatusCode.OK, (await holder.GetAsync(Probe)).StatusCode);

        (await UpdateRole(role, isActive: true)).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Forbidden, (await holder.GetAsync(Probe)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await holder.GetAsync(RequisitionsProbe)).StatusCode);
    }

    [Fact]
    public async Task A_System_Role_Holder_Still_Gets_Their_Matrix()
    {
        // The fix must not touch the ordinary path: a Recruiter with no custom role.
        var res = await _scenario.Client(Roles.Recruiter, _factory.RecruiterUserId).GetAsync(RequisitionsProbe);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }
}
