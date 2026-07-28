using System.Net;
using System.Net.Http.Json;
using RecruitOps.Api.Auth;
using RecruitOps.Application.DTOs;
using Xunit;

namespace RecruitOps.Api.Tests;

/// <summary>Department scoping (ADR-0003). This is the security-critical filter: unlike
/// tenant isolation it is NOT enforced by an EF query filter, so it can be forgotten —
/// these tests are what keep it honest.</summary>
public class RequisitionScopingTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;

    public RequisitionScopingTests(CustomWebAppFactory factory) => _factory = factory;

    private HttpClient ClientFor(string role, Guid? userId = null)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Tenant", _factory.TenantA.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", role);
        if (userId is not null)
            client.DefaultRequestHeaders.Add("X-Test-UserId", userId.Value.ToString());
        return client;
    }

    [Fact]
    public async Task Recruiter_Sees_Requisitions_From_All_Departments()
    {
        var res = await ClientFor(Roles.Recruiter).GetAsync("/api/requisitions");
        res.EnsureSuccessStatusCode();

        var items = await res.Content.ReadFromJsonAsync<List<RequisitionListItemDto>>();
        Assert.NotNull(items);
        Assert.Contains(items!, r => r.Title == "Sales Executive");
        Assert.Contains(items!, r => r.Title == "Financial Analyst");
    }

    [Fact]
    public async Task HiringManager_Sees_Only_Their_Own_Department()
    {
        var res = await ClientFor(Roles.HiringManager, _factory.HiringManagerUserId)
            .GetAsync("/api/requisitions");
        res.EnsureSuccessStatusCode();

        var items = await res.Content.ReadFromJsonAsync<List<RequisitionListItemDto>>();
        Assert.NotNull(items);
        Assert.Single(items!);
        Assert.Equal("Sales Executive", items![0].Title);
        // The Finance requisition belongs to a department they do not own.
        Assert.DoesNotContain(items, r => r.Title == "Financial Analyst");
    }

    [Fact]
    public async Task HiringManager_Gets_404_For_Another_Departments_Requisition()
    {
        // Find the Finance requisition as a recruiter (who can see everything)...
        var all = await (await ClientFor(Roles.Recruiter).GetAsync("/api/requisitions"))
            .Content.ReadFromJsonAsync<List<RequisitionListItemDto>>();
        var finance = all!.Single(r => r.Title == "Financial Analyst");

        // ...then try to read it as the Sales manager.
        var res = await ClientFor(Roles.HiringManager, _factory.HiringManagerUserId)
            .GetAsync($"/api/requisitions/{finance.Id}");

        // 404 rather than 403 — do not leak that the row exists.
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task HiringManager_Cannot_Create_In_Another_Department()
    {
        var all = await (await ClientFor(Roles.Recruiter).GetAsync("/api/requisitions"))
            .Content.ReadFromJsonAsync<List<RequisitionListItemDto>>();
        var financeDeptId = all!.Single(r => r.Title == "Financial Analyst").DepartmentId;

        var res = await ClientFor(Roles.HiringManager, _factory.HiringManagerUserId)
            .PostAsJsonAsync("/api/requisitions", new CreateRequisitionRequest
            {
                DepartmentId = financeDeptId,
                Title = "Sneaky Hire",
                JobDescription = "Should never be created.",
                Headcount = 1,
            });

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task HiringManager_Can_Create_In_Their_Own_Department()
    {
        var res = await ClientFor(Roles.HiringManager, _factory.HiringManagerUserId)
            .PostAsJsonAsync("/api/requisitions", new CreateRequisitionRequest
            {
                DepartmentId = _factory.SalesDepartmentId,
                Title = "Sales Trainee",
                JobDescription = "Learn to sell things.",
                Headcount = 3,
            });

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var created = await res.Content.ReadFromJsonAsync<RequisitionDetailDto>();
        Assert.NotNull(created);
        Assert.Equal("Draft", created!.Status);
        Assert.Equal("Alpha Sales", created.DepartmentName);
        // Creating stamps the caller as the requester — this is what gates Cancel later.
        Assert.Equal(_factory.HiringManagerUserId, created.RequestedByUserId);
        Assert.Empty(created.Approvals); // a Draft has no chain snapshot yet
    }

    [Fact]
    public async Task Unauthenticated_Request_Is_401()
    {
        var res = await _factory.CreateClient().GetAsync("/api/requisitions");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
