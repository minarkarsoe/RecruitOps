using System.Net;
using System.Net.Http.Json;
using RecruitOps.Api.Auth;
using RecruitOps.Application.DTOs;
using Xunit;

namespace RecruitOps.Api.Tests;

public class RolesAndPermissionsApiTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;
    private readonly Module3Scenario _scenario;

    public RolesAndPermissionsApiTests(CustomWebAppFactory factory)
    {
        _factory = factory;
        _scenario = new Module3Scenario(factory);
    }

    private HttpClient AdminClient() => _scenario.Client(Roles.Admin, _factory.AdminUserId);
    private HttpClient RecruiterClient() => _scenario.Client(Roles.Recruiter, _factory.AdminUserId);
    private HttpClient HiringManagerClient() => _scenario.Client(Roles.HiringManager, _factory.HiringManagerUserId);

    [Fact]
    public async Task Get_Permissions_Returns_Grouped_Modules_And_Features()
    {
        var client = AdminClient();
        var response = await client.GetAsync("/api/permissions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var modules = await response.Content.ReadFromJsonAsync<List<PermissionModuleDto>>();
        Assert.NotNull(modules);
        Assert.NotEmpty(modules);
        Assert.Contains(modules, m => m.Module == "requisitions");
        Assert.Contains(modules, m => m.Module == "roles");
        Assert.Contains(modules, m => m.Module == "users");
    }

    [Fact]
    public async Task Get_Roles_Returns_System_Roles()
    {
        var client = AdminClient();
        var response = await client.GetAsync("/api/roles");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var roles = await response.Content.ReadFromJsonAsync<List<RoleListItemDto>>();
        Assert.NotNull(roles);
        Assert.NotEmpty(roles);
        Assert.Contains(roles, r => r.Code == "Admin" && r.IsSystemRole);
        Assert.Contains(roles, r => r.Code == "Recruiter" && r.IsSystemRole);
    }

    [Fact]
    public async Task Get_Role_ById_Returns_Details_For_Existing_Role()
    {
        var client = AdminClient();
        var roles = await client.GetFromJsonAsync<List<RoleListItemDto>>("/api/roles");
        var adminRole = roles!.First(r => r.Code == "Admin");

        var response = await client.GetAsync($"/api/roles/{adminRole.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var detail = await response.Content.ReadFromJsonAsync<RoleDetailDto>();
        Assert.NotNull(detail);
        Assert.Equal("Admin", detail.Code);
        Assert.True(detail.IsSystemRole);
        Assert.NotEmpty(detail.AssignedPermissionCodes);
    }

    [Fact]
    public async Task Get_Role_ById_Returns_404_For_NonExistent_Id()
    {
        var client = AdminClient();
        var response = await client.GetAsync($"/api/roles/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_Role_Succeeds_For_Valid_Request()
    {
        var client = AdminClient();
        var request = new CreateRoleRequest(
            Name: $"Test Custom Role {Guid.NewGuid():N}",
            Code: $"CUSTOM_ROLE_{Guid.NewGuid():N}",
            Description: "Test Custom Role Description",
            PermissionCodes: new[] { "permission:requisitions:requisitions:read", "permission:postings:postings:read" }
        );

        var response = await client.PostAsJsonAsync("/api/roles", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<RoleDetailDto>();
        Assert.NotNull(created);
        Assert.Equal(request.Name, created.Name);
        Assert.False(created.IsSystemRole);
        Assert.Contains("permission:requisitions:requisitions:read", created.AssignedPermissionCodes);
    }

    [Fact]
    public async Task Create_Role_Fails_When_Name_Already_Exists()
    {
        var client = AdminClient();
        var request = new CreateRoleRequest(
            Name: "Admin",
            Code: "ADMIN_DUPLICATE",
            Description: "Duplicate Admin Name",
            PermissionCodes: new string[0]
        );

        var response = await client.PostAsJsonAsync("/api/roles", request);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Create_Role_Fails_When_Invalid_Permission_Code()
    {
        var client = AdminClient();
        var request = new CreateRoleRequest(
            Name: $"Invalid Perm Role {Guid.NewGuid():N}",
            Code: null,
            Description: null,
            PermissionCodes: new[] { "permission:invalid:code:that:does:not:exist" }
        );

        var response = await client.PostAsJsonAsync("/api/roles", request);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Update_Role_Fails_For_System_Role()
    {
        var client = AdminClient();
        var roles = await client.GetFromJsonAsync<List<RoleListItemDto>>("/api/roles");
        var adminRole = roles!.First(r => r.IsSystemRole);

        var updateRequest = new UpdateRoleRequest(
            Name: "Attempted Update System Role",
            Description: "New Description",
            IsActive: true,
            PermissionCodes: new[] { "permission:requisitions:requisitions:read" }
        );

        var response = await client.PutAsJsonAsync($"/api/roles/{adminRole.Id}", updateRequest);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_Role_Succeeds_For_Custom_Role()
    {
        var client = AdminClient();
        var createRequest = new CreateRoleRequest(
            Name: $"Updatable Custom Role {Guid.NewGuid():N}",
            Code: null,
            Description: "Before Update",
            PermissionCodes: new[] { "permission:requisitions:requisitions:read" }
        );

        var createRes = await client.PostAsJsonAsync("/api/roles", createRequest);
        var created = await createRes.Content.ReadFromJsonAsync<RoleDetailDto>();

        var updateRequest = new UpdateRoleRequest(
            Name: $"{created!.Name} Updated",
            Description: "After Update",
            IsActive: true,
            PermissionCodes: new[] { "permission:postings:postings:read", "permission:postings:postings:create" }
        );

        var updateRes = await client.PutAsJsonAsync($"/api/roles/{created.Id}", updateRequest);
        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);

        var updated = await updateRes.Content.ReadFromJsonAsync<RoleDetailDto>();
        Assert.Equal(updateRequest.Name, updated!.Name);
        Assert.Equal("After Update", updated.Description);
        Assert.Contains("permission:postings:postings:read", updated.AssignedPermissionCodes);
    }

    [Fact]
    public async Task Delete_Role_Fails_For_System_Role()
    {
        var client = AdminClient();
        var roles = await client.GetFromJsonAsync<List<RoleListItemDto>>("/api/roles");
        var adminRole = roles!.First(r => r.IsSystemRole);

        var response = await client.DeleteAsync($"/api/roles/{adminRole.Id}");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Delete_Role_Succeeds_For_Unassigned_Custom_Role()
    {
        var client = AdminClient();
        var createRequest = new CreateRoleRequest(
            Name: $"Deletable Role {Guid.NewGuid():N}",
            Code: null,
            Description: "To be deleted",
            PermissionCodes: new string[0]
        );

        var createRes = await client.PostAsJsonAsync("/api/roles", createRequest);
        var created = await createRes.Content.ReadFromJsonAsync<RoleDetailDto>();

        var deleteRes = await client.DeleteAsync($"/api/roles/{created!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteRes.StatusCode);

        var getRes = await client.GetAsync($"/api/roles/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getRes.StatusCode);
    }

    [Fact]
    public async Task Roles_Endpoints_Deny_Access_To_HiringManager_Without_Permission()
    {
        var client = HiringManagerClient();

        var getRes = await client.GetAsync("/api/roles");
        Assert.Equal(HttpStatusCode.Forbidden, getRes.StatusCode);

        var postRes = await client.PostAsJsonAsync("/api/roles", new CreateRoleRequest("Forbidden Role", null, null, new string[0]));
        Assert.Equal(HttpStatusCode.Forbidden, postRes.StatusCode);
    }
}
