using System.Net;
using System.Net.Http.Json;
using RecruitOps.Api.Auth;
using RecruitOps.Application.DTOs;
using Xunit;

namespace RecruitOps.Api.Tests;

/// <summary>Proves tenant isolation end-to-end through the real auth + EF query-filter
/// pipeline. Tenant filters are a dormant safety net (ADR-0004) — these tests are what
/// keep them honest.</summary>
public class DepartmentIsolationTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;

    public DepartmentIsolationTests(CustomWebAppFactory factory) => _factory = factory;

    private HttpClient ClientFor(Guid tenant, string roles = Roles.Admin)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Tenant", tenant.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", roles);
        return client;
    }

    [Fact]
    public async Task Unauthenticated_Request_Is_401()
    {
        var res = await _factory.CreateClient().GetAsync("/api/departments");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task TenantA_Sees_Only_Its_Own_Departments()
    {
        var res = await ClientFor(_factory.TenantA).GetAsync("/api/departments");
        res.EnsureSuccessStatusCode();

        var items = await res.Content.ReadFromJsonAsync<List<DepartmentListItemDto>>();
        Assert.NotNull(items);
        // TenantA owns exactly its own two departments — and nothing of TenantB's.
        Assert.Equal(2, items!.Count);
        Assert.Contains(items, i => i.Name == "Alpha Sales");
        Assert.Contains(items, i => i.Name == "Alpha Finance");
        Assert.DoesNotContain(items, i => i.Name == "Bravo Finance");
    }

    [Fact]
    public async Task TenantB_Cannot_See_TenantA_Departments()
    {
        var res = await ClientFor(_factory.TenantB).GetAsync("/api/departments");
        res.EnsureSuccessStatusCode();

        var items = await res.Content.ReadFromJsonAsync<List<DepartmentListItemDto>>();
        Assert.NotNull(items);
        Assert.DoesNotContain(items!, i => i.Name == "Alpha Sales");
    }

    [Fact]
    public async Task HiringManager_Is_Forbidden_From_CrossDepartment_Endpoint()
    {
        // This used to assert 403 on GET /api/departments — which was wrong in a way that
        // broke the product: that endpoint feeds the department picker on the
        // new-requisition form, so a Hiring Manager could never raise a requisition through
        // the UI. The list is now scoped per ADR-0003 instead of blocked (see
        // DepartmentAdminTests), and the RBAC boundary this test guards moved to the admin
        // view, which really is cross-department.
        var res = await ClientFor(_factory.TenantA, Roles.HiringManager).GetAsync("/api/departments/admin");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }
}
