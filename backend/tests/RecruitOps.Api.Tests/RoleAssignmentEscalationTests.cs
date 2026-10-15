using System.Net;
using System.Net.Http.Json;
using RecruitOps.Api.Auth;
using RecruitOps.Application.DTOs;
using Xunit;

namespace RecruitOps.Api.Tests;

/// <summary>Who may be given which role through the tenant user directory (ADR-0021).
/// <para>Found 2026-10-15 by driving the running stack, not by this suite: an Admin could create
/// a <c>SuperAdmin</c> — the cross-tenant bypass role — by sending <c>"role": "SuperAdmin"</c>,
/// or <c>"0"</c>, because <c>Enum.TryParse</c> accepts any enum name <i>and</i> any numeric
/// string. A custom role coded <c>"0"</c> did the same on assignment, and a custom role of any
/// other code silently became <c>Recruiter</c>: company-wide reach into every candidate, whatever
/// its permission matrix said. The pre-existing tests never noticed because none of them asked
/// what role a user <i>ended up</i> with.</para>
/// <para>Each refusal here is paired with a case proving the legitimate assignment still works —
/// a suite that only checks refusals stays green over an endpoint nobody can use.</para></summary>
public class RoleAssignmentEscalationTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;
    private readonly Module3Scenario _scenario;

    public RoleAssignmentEscalationTests(CustomWebAppFactory factory)
    {
        _factory = factory;
        _scenario = new Module3Scenario(factory);
    }

    private HttpClient Admin() => _scenario.Client(Roles.Admin, _factory.AdminUserId);

    private static string UniqueEmail() => $"user.{Guid.NewGuid():N}@alpha.test";

    private Task<HttpResponseMessage> CreateUser(string? role = null, Guid? roleId = null) =>
        Admin().PostAsJsonAsync("/api/users",
            new CreateUserRequest(UniqueEmail(), "Directory Person", "P@ssw0rd-Test-123", roleId, role));

    private async Task<RoleDetailDto> SystemRole(string code)
    {
        var roles = await Admin().GetFromJsonAsync<List<RoleDetailDto>>("/api/roles");
        return roles!.Single(r => r.IsSystemRole && r.Code == code);
    }

    // ---------------------------------------------------------------- refusals

    [Theory]
    [InlineData("SuperAdmin")]
    [InlineData("superadmin")]
    [InlineData("0")]   // UserRole.SuperAdmin's underlying value
    [InlineData("1")]   // numeric parsing is refused outright, not just for SuperAdmin
    [InlineData("99")]  // undefined value — Enum.TryParse accepts it and yields (UserRole)99
    [InlineData(" 0")]
    [InlineData("00")]
    [InlineData("-1")]
    [InlineData("Admin,Recruiter")] // TryParse ORs flag-style lists together
    [InlineData(" superadmin ")]
    [InlineData("")]                // no role at all — it used to default to Recruiter
    public async Task Creating_A_User_Cannot_Name_SuperAdmin_Or_A_Numeric_Role(string role)
    {
        var res = await CreateUser(role: role);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task An_Existing_User_Cannot_Be_Promoted_To_SuperAdmin()
    {
        var created = await (await CreateUser(role: "Recruiter")).Content.ReadFromJsonAsync<UserDetailDto>();

        foreach (var role in new[] { "SuperAdmin", "0" })
        {
            var res = await Admin().PutAsJsonAsync($"/api/users/{created!.Id}",
                new UpdateUserRequest("Directory Person", null, role));
            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        }

        var after = await Admin().GetFromJsonAsync<UserDetailDto>($"/api/users/{created!.Id}");
        Assert.Equal("Recruiter", after!.Role);
        Assert.False(after.IsSuperAdmin);
    }

    [Fact]
    public async Task The_SuperAdmin_System_Role_Cannot_Be_Assigned_By_Id()
    {
        var superAdmin = await SystemRole("SuperAdmin");
        var res = await CreateUser(roleId: superAdmin.Id);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task The_SuperAdmin_System_Role_Cannot_Be_Given_To_An_Existing_User_By_Id()
    {
        var superAdmin = await SystemRole("SuperAdmin");
        var created = await (await CreateUser(role: "Recruiter")).Content.ReadFromJsonAsync<UserDetailDto>();

        var res = await Admin().PutAsJsonAsync($"/api/users/{created!.Id}",
            new UpdateUserRequest("Directory Person", superAdmin.Id, null));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task A_Custom_Role_Code_Must_Start_With_A_Letter()
    {
        var res = await Admin().PostAsJsonAsync("/api/roles",
            new CreateRoleRequest($"Numeric {Guid.NewGuid():N}", "0", null, Array.Empty<string>()));
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    /// <summary>Custom roles are assignable on develop (ADR-0022: a custom role holding
    /// <c>requisitions:approve</c> is how a tenant configures its approvers). What is pinned here is
    /// the escalation: whatever a custom role is called or coded, its holder is never a SuperAdmin.
    /// The enum a custom-role holder gets (Recruiter) is the open ADR-0021 question — asserted so a
    /// change to it is deliberate.</summary>
    [Fact]
    public async Task A_Custom_Role_Is_Assignable_And_Never_Yields_SuperAdmin()
    {
        var customRes = await Admin().PostAsJsonAsync("/api/roles",
            new CreateRoleRequest($"Approver Lite {Guid.NewGuid():N}", null, "Approves only",
                new[] { "permission:requisitions:requisitions:approve" }));
        customRes.EnsureSuccessStatusCode();
        var custom = (await customRes.Content.ReadFromJsonAsync<RoleDetailDto>())!;

        var created = await CreateUser(roleId: custom.Id);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var user = (await created.Content.ReadFromJsonAsync<UserDetailDto>())!;

        Assert.Equal(custom.Id, user.RoleId);
        Assert.False(user.IsSuperAdmin);
        Assert.NotEqual("SuperAdmin", user.Role);
        Assert.Equal("Recruiter", user.Role); // ADR-0021 (open): the enum a custom role holder gets
    }

    // ------------------------------------------------- what the feature is for

    [Theory]
    [InlineData("Admin")]
    [InlineData("HrDirector")]
    [InlineData("Recruiter")]
    [InlineData("HiringManager")]
    [InlineData("Approver")]
    [InlineData("Interviewer")]
    public async Task Each_Tenant_System_Role_Can_Be_Assigned_By_Name(string role)
    {
        var res = await CreateUser(role: role);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);

        var user = (await res.Content.ReadFromJsonAsync<UserDetailDto>())!;
        Assert.Equal(role, user.Role);
        Assert.Equal((await SystemRole(role)).Id, user.RoleId);
        Assert.False(user.IsSuperAdmin);
    }

    [Fact]
    public async Task A_System_Role_Can_Be_Assigned_By_Id()
    {
        var hiringManager = await SystemRole("HiringManager");

        var res = await CreateUser(roleId: hiringManager.Id);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);

        var user = (await res.Content.ReadFromJsonAsync<UserDetailDto>())!;
        Assert.Equal("HiringManager", user.Role);
        Assert.Equal(hiringManager.Id, user.RoleId);
    }
}
