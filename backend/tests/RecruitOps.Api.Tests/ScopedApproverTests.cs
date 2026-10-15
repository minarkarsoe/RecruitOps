using System.Net;
using System.Net.Http.Json;
using RecruitOps.Api.Auth;
using RecruitOps.Application.DTOs;
using Xunit;

namespace RecruitOps.Api.Tests;

/// <summary>An approver named on a requisition's chain sees what they are asked to approve, even
/// when their role is department-scoped (ADR-0003: "an approver must see what they approve").
/// <para>Found by the re-review of ADR-0021 option A: once Interviewer became department-scoped,
/// an Interviewer-based approver named on another department's chain could still find the
/// requisition in their inbox and decide it — the decision was saved — but the response was a
/// 404, because the decision returns the requisition through the department check. A Hiring
/// Manager approving another department's request had the same problem before. Its own factory:
/// it creates a department-specific chain, which would change other classes' chains.</para></summary>
public class ScopedApproverTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;
    private readonly Module3Scenario _scenario;

    public ScopedApproverTests(CustomWebAppFactory factory)
    {
        _factory = factory;
        _scenario = new Module3Scenario(factory);
    }

    [Fact]
    public async Task A_Department_Scoped_Approver_Can_Decide_And_Read_What_They_Approve()
    {
        var admin = _scenario.Client(Roles.Admin, _factory.AdminUserId);

        var roleRes = await admin.PostAsJsonAsync("/api/roles", new CreateRoleRequest(
            $"Gate {Guid.NewGuid():N}", null, "approve only, narrowest base",
            new[] { "permission:requisitions:requisitions:approve", "permission:requisitions:requisitions:read" },
            "Interviewer"));
        roleRes.EnsureSuccessStatusCode();
        var role = (await roleRes.Content.ReadFromJsonAsync<RoleDetailDto>())!;

        var userRes = await admin.PostAsJsonAsync("/api/users", new CreateUserRequest(
            $"gate.{Guid.NewGuid():N}@alpha.test", "Scoped Gate", "P@ssw0rd-Test-123", role.Id));
        userRes.EnsureSuccessStatusCode();
        var approver = (await userRes.Content.ReadFromJsonAsync<UserDetailDto>())!;

        (await admin.PostAsJsonAsync("/api/approvalchains", new CreateApprovalChainRequest
        {
            Name = $"Finance gate {Guid.NewGuid():N}",
            DepartmentId = _factory.FinanceDepartmentId,
            Steps = new[] { new CreateApprovalChainStepRequest { ApproverUserId = approver.Id, Label = "Gate" } },
        })).EnsureSuccessStatusCode();

        var manager = _scenario.Client(Roles.HiringManager, _factory.FinanceManagerUserId);
        var draft = (await (await manager.PostAsJsonAsync("/api/requisitions", new CreateRequisitionRequest
        {
            DepartmentId = _factory.FinanceDepartmentId,
            Title = "Treasury Analyst",
            JobDescription = "Needs the gate.",
            Headcount = 1,
        })).Content.ReadFromJsonAsync<RequisitionDetailDto>())!;
        (await manager.PostAsync($"/api/requisitions/{draft.Id}/submit", null)).EnsureSuccessStatusCode();

        var gate = _scenario.Client("Interviewer", approver.Id);

        // Named on the chain: may read it, though Finance is not one of their departments.
        Assert.Equal(HttpStatusCode.OK, (await gate.GetAsync($"/api/requisitions/{draft.Id}")).StatusCode);

        var decided = await gate.PostAsJsonAsync($"/api/requisitions/{draft.Id}/decision",
            new ApprovalDecisionRequest { Approve = true, Comment = "ok" });
        Assert.Equal(HttpStatusCode.OK, decided.StatusCode);
        Assert.Equal("Approved", (await decided.Content.ReadFromJsonAsync<RequisitionDetailDto>())!.Status);

        // Still scoped for everything else: the list shows nothing outside their departments.
        Assert.DoesNotContain((await gate.GetFromJsonAsync<List<RequisitionListItemDto>>("/api/requisitions"))!,
            r => r.Id != draft.Id);
    }
}
