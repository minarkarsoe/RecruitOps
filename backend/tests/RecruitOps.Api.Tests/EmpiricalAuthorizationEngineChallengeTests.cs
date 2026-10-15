using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using RecruitOps.Api.Auth;
using RecruitOps.Application.DTOs;
using Xunit;

namespace RecruitOps.Api.Tests;

public class EmpiricalAuthorizationEngineChallengeTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;

    public EmpiricalAuthorizationEngineChallengeTests(CustomWebAppFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClientForTenant(Guid tenantId, string role, Guid? userId = null, bool isSuperAdmin = false)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Tenant", tenantId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", role);
        if (userId.HasValue)
        {
            client.DefaultRequestHeaders.Add("X-Test-UserId", userId.Value.ToString());
        }
        if (isSuperAdmin)
        {
            client.DefaultRequestHeaders.Add("X-Test-IsSuperAdmin", "true");
        }
        return client;
    }

    // =========================================================================
    // Scope 1: System Role Protection
    // =========================================================================

    [Fact]
    public async Task Update_System_Role_Is_Strictly_Blocked_With_HTTP_400_BadRequest()
    {
        var client = CreateClientForTenant(_factory.TenantA, Roles.Admin, _factory.AdminUserId);

        var roles = await client.GetFromJsonAsync<List<RoleListItemDto>>("/api/roles");
        Assert.NotNull(roles);

        var systemRoles = roles.Where(r => r.IsSystemRole).ToList();
        Assert.NotEmpty(systemRoles);

        foreach (var sysRole in systemRoles)
        {
            var updateRequest = new UpdateRoleRequest(
                Name: $"Attempted Renaming {sysRole.Name}",
                Description: "Malicious update attempt on system role",
                IsActive: true,
                PermissionCodes: new[] { "permission:requisitions:requisitions:read" }
            );

            var response = await client.PutAsJsonAsync($"/api/roles/{sysRole.Id}", updateRequest);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            Assert.NotNull(problem);
            Assert.Contains("System roles are pre-configured and immutable", problem.Detail, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Delete_System_Role_Is_Strictly_Blocked_With_HTTP_Conflict_Or_BadRequest()
    {
        var client = CreateClientForTenant(_factory.TenantA, Roles.Admin, _factory.AdminUserId);

        var roles = await client.GetFromJsonAsync<List<RoleListItemDto>>("/api/roles");
        Assert.NotNull(roles);

        var systemRoles = roles.Where(r => r.IsSystemRole).ToList();
        Assert.NotEmpty(systemRoles);

        foreach (var sysRole in systemRoles)
        {
            var response = await client.DeleteAsync($"/api/roles/{sysRole.Id}");

            // Verification: System role deletion must be blocked (HTTP 409 Conflict or 400 BadRequest)
            Assert.True(response.StatusCode == HttpStatusCode.Conflict || response.StatusCode == HttpStatusCode.BadRequest,
                $"Expected 409 Conflict or 400 BadRequest, but got {response.StatusCode} for role '{sysRole.Name}'");

            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            Assert.NotNull(problem);
            Assert.Contains("Pre-configured system roles cannot be deleted", problem.Detail, StringComparison.OrdinalIgnoreCase);
        }
    }

    // =========================================================================
    // Scope 2: Tenant Isolation
    // =========================================================================

    [Fact]
    public async Task Custom_Roles_Created_By_TenantA_Are_Isolated_From_TenantB()
    {
        var tenantAClient = CreateClientForTenant(_factory.TenantA, Roles.Admin, _factory.AdminUserId);
        // No user id: TenantB has no seeded admin, and passing a random GUID would name a
        // user that does not exist. That used to "work" only because a denied database
        // lookup fell through to the role-claim fallback — removed in ADR-0022, since it let
        // a custom role (and a deactivated user) inherit a seeded role's permissions. This
        // test is about tenant isolation, not user resolution, so it takes the legitimate
        // identity-less path and lets the seeded Admin role authorize the call.
        var tenantBClient = CreateClientForTenant(_factory.TenantB, Roles.Admin);

        // Step 1: Tenant A creates a custom role
        var roleName = $"TenantA Custom Role {Guid.NewGuid():N}";
        var createReq = new CreateRoleRequest(
            Name: roleName,
            Code: null,
            Description: "Tenant A secret custom role",
            PermissionCodes: new[] { "permission:requisitions:requisitions:read" }
        );

        var createRes = await tenantAClient.PostAsJsonAsync("/api/roles", createReq);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var tenantARole = await createRes.Content.ReadFromJsonAsync<RoleDetailDto>();
        Assert.NotNull(tenantARole);

        // Step 2: Tenant B attempts to read Tenant A's custom role by ID -> 404 NotFound
        var getByIdRes = await tenantBClient.GetAsync($"/api/roles/{tenantARole.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getByIdRes.StatusCode);

        // Step 3: Tenant B attempts to update Tenant A's custom role by ID -> 404 NotFound
        var updateReq = new UpdateRoleRequest(
            Name: "Hijacked Role Name",
            Description: "Hijacked Description",
            IsActive: true,
            PermissionCodes: new[] { "permission:roles:roles:read" }
        );
        var updateRes = await tenantBClient.PutAsJsonAsync($"/api/roles/{tenantARole.Id}", updateReq);
        Assert.Equal(HttpStatusCode.NotFound, updateRes.StatusCode);

        // Step 4: Tenant B attempts to delete Tenant A's custom role by ID -> 404 NotFound
        var deleteRes = await tenantBClient.DeleteAsync($"/api/roles/{tenantARole.Id}");
        Assert.Equal(HttpStatusCode.NotFound, deleteRes.StatusCode);

        // Step 5: Tenant B lists all roles -> Tenant A's custom role is absent
        var tenantBRoles = await tenantBClient.GetFromJsonAsync<List<RoleListItemDto>>("/api/roles");
        Assert.NotNull(tenantBRoles);
        Assert.DoesNotContain(tenantBRoles, r => r.Id == tenantARole.Id || r.Name == roleName);
    }

    // =========================================================================
    // Scope 3: Permission Claim Authorization
    // =========================================================================

    [Fact]
    public async Task Permission_Claim_Authorization_Allows_Permitted_Endpoint_And_Rejects_Missing_Permission_With_403()
    {
        // 1. HiringManager has permission:requisitions:requisitions:read but lacks permission:roles:roles:read & permission:roles:roles:create
        var hiringManagerClient = CreateClientForTenant(_factory.TenantA, Roles.HiringManager, _factory.HiringManagerUserId);

        // Allowed: GET /api/requisitions -> 200 OK
        var requisitionsRes = await hiringManagerClient.GetAsync("/api/requisitions");
        Assert.Equal(HttpStatusCode.OK, requisitionsRes.StatusCode);

        // Forbidden: GET /api/roles -> 403 Forbidden
        var rolesGetRes = await hiringManagerClient.GetAsync("/api/roles");
        Assert.Equal(HttpStatusCode.Forbidden, rolesGetRes.StatusCode);

        // Forbidden: POST /api/roles -> 403 Forbidden
        var rolesPostRes = await hiringManagerClient.PostAsJsonAsync("/api/roles", new CreateRoleRequest(
            Name: "Unauthorized Role",
            Code: null,
            Description: null,
            PermissionCodes: new string[0]
        ));
        Assert.Equal(HttpStatusCode.Forbidden, rolesPostRes.StatusCode);
    }

    // =========================================================================
    // Scope 4: Super-Admin Bypass
    // =========================================================================

    [Fact]
    public async Task SuperAdmin_Bypasses_Permission_Checks_Regardless_Of_Specific_Claims()
    {
        // SuperAdmin user with no specific role claims assigned
        var superAdminUserGuid = Guid.NewGuid();
        var superAdminClient = CreateClientForTenant(_factory.TenantA, "NoRoleUser", superAdminUserGuid, isSuperAdmin: true);

        // SuperAdmin reaches roles list without explicit permission claim
        var getRolesRes = await superAdminClient.GetAsync("/api/roles");
        Assert.Equal(HttpStatusCode.OK, getRolesRes.StatusCode);

        // SuperAdmin reaches permissions list without explicit permission claim
        var getPermsRes = await superAdminClient.GetAsync("/api/permissions");
        Assert.Equal(HttpStatusCode.OK, getPermsRes.StatusCode);

        // SuperAdmin creates a custom role without explicit permission claim
        var createRoleReq = new CreateRoleRequest(
            Name: $"SuperAdmin Created Role {Guid.NewGuid():N}",
            Code: null,
            Description: "Bypass test role",
            PermissionCodes: new[] { "permission:requisitions:requisitions:read" }
        );
        var createRoleRes = await superAdminClient.PostAsJsonAsync("/api/roles", createRoleReq);
        Assert.Equal(HttpStatusCode.Created, createRoleRes.StatusCode);
    }
}
