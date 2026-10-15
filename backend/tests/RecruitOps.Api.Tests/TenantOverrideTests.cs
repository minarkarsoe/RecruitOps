using System.Net;
using System.Net.Http.Json;
using RecruitOps.Api.Auth;
using RecruitOps.Application.DTOs;
using Xunit;

namespace RecruitOps.Api.Tests;

/// <summary>`X-Tenant-Id` — super-admin tenant switching, through the real pipeline.
///
/// <para><c>CurrentTenantResolutionTests</c> pins the resolution rule in isolation. This file
/// asks the question that actually matters: <b>does an ordinary user who sets the header get
/// another company's rows back?</b> A unit test of the resolver cannot answer that, because the
/// answer depends on the resolver, the middleware, the query filters and the endpoint agreeing.
///
/// <para>The header was sent by the SPA and read by nothing until 2026-08-26. Wiring it up is a
/// deliberate hole in tenant isolation for exactly one role, so the negative cases below carry as
/// much weight as the positive one.</para>
/// </summary>
public class TenantOverrideTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;

    public TenantOverrideTests(CustomWebAppFactory factory) => _factory = factory;

    private HttpClient Client(Guid tenant, string role, bool superAdmin = false, Guid? overrideTenant = null)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Tenant", tenant.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", role);
        client.DefaultRequestHeaders.Add("X-Test-UserId", _factory.AdminUserId.ToString());
        if (superAdmin)
            client.DefaultRequestHeaders.Add("X-Test-IsSuperAdmin", "true");
        if (overrideTenant is not null)
            client.DefaultRequestHeaders.Add(CurrentTenant.TenantOverrideHeader, overrideTenant.Value.ToString());
        return client;
    }

    private async Task<List<DepartmentListItemDto>> DepartmentsAsync(HttpClient client)
    {
        var res = await client.GetAsync("/api/departments");
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<List<DepartmentListItemDto>>())!;
    }

    [Fact]
    public async Task Without_The_Header_An_Admin_Sees_Only_Their_Own_Company()
    {
        var departments = await DepartmentsAsync(Client(_factory.TenantA, Roles.Admin));

        Assert.Contains(departments, d => d.Name == "Alpha Sales");
        Assert.DoesNotContain(departments, d => d.Name == "Bravo Finance");
    }

    [Fact]
    public async Task A_Super_Admin_Sees_The_Other_Company_When_They_Ask_For_It()
    {
        // The capability, end to end: same token, one header, different company's rows.
        var client = Client(_factory.TenantA, Roles.Admin, superAdmin: true, overrideTenant: _factory.TenantB);

        var departments = await DepartmentsAsync(client);

        Assert.Contains(departments, d => d.Name == "Bravo Finance");
        Assert.DoesNotContain(departments, d => d.Name == "Alpha Sales");
    }

    [Fact]
    public async Task An_Ordinary_Admin_Sending_The_Header_Still_Sees_Only_Their_Own_Company()
    {
        // ⚠️ The test this whole feature is judged on. Identical request to the one above minus
        // the super-admin claim. If this ever returns Bravo's rows, every authenticated user in
        // the product can read every company's data by setting one header.
        var client = Client(_factory.TenantA, Roles.Admin, superAdmin: false, overrideTenant: _factory.TenantB);

        var departments = await DepartmentsAsync(client);

        Assert.Contains(departments, d => d.Name == "Alpha Sales");
        Assert.DoesNotContain(departments, d => d.Name == "Bravo Finance");
    }

    [Fact]
    public async Task A_Recruiter_Sending_The_Header_Is_Also_Ignored()
    {
        // Not just Admin — the gate is the super-admin claim, not the role's seniority.
        var client = Client(_factory.TenantA, Roles.Recruiter, superAdmin: false, overrideTenant: _factory.TenantB);

        var departments = await DepartmentsAsync(client);

        Assert.DoesNotContain(departments, d => d.Name == "Bravo Finance");
    }

    [Fact]
    public async Task A_Super_Admin_Asking_For_A_Company_That_Does_Not_Exist_Gets_A_400()
    {
        // Rather than a silently empty app, which reads exactly like data loss.
        var client = Client(_factory.TenantA, Roles.Admin, superAdmin: true, overrideTenant: Guid.NewGuid());

        var res = await client.GetAsync("/api/departments");

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task A_Super_Admin_Without_The_Header_Stays_On_Their_Own_Company()
    {
        var departments = await DepartmentsAsync(Client(_factory.TenantA, Roles.Admin, superAdmin: true));

        Assert.Contains(departments, d => d.Name == "Alpha Sales");
        Assert.DoesNotContain(departments, d => d.Name == "Bravo Finance");
    }

    [Fact]
    public async Task The_Tenant_List_Is_Invisible_To_Everyone_But_A_Super_Admin()
    {
        var ordinary = await Client(_factory.TenantA, Roles.Admin).GetAsync("/api/tenants");

        // 404, not 403: a 403 would confirm the list is there to be read.
        Assert.Equal(HttpStatusCode.NotFound, ordinary.StatusCode);
    }

    [Fact]
    public async Task The_Tenant_List_Names_Every_Active_Company()
    {
        var client = Client(_factory.TenantA, Roles.Admin, superAdmin: true);

        var res = await client.GetAsync("/api/tenants");
        res.EnsureSuccessStatusCode();
        var tenants = (await res.Content.ReadFromJsonAsync<List<TenantRow>>())!;

        // Both, regardless of which one the caller is currently viewing — that is what makes it a
        // switcher rather than a description of where you already are.
        Assert.Contains(tenants, t => t.Id == _factory.TenantA);
        Assert.Contains(tenants, t => t.Id == _factory.TenantB);
        Assert.All(tenants, t => Assert.False(string.IsNullOrWhiteSpace(t.Name)));
    }

    private sealed record TenantRow(Guid Id, string Name, string Code, bool IsActive);
}
