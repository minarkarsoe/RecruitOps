using System.Net;
using System.Net.Http.Json;
using RecruitOps.Api.Auth;
using RecruitOps.Application.DTOs;
using Xunit;

namespace RecruitOps.Api.Tests;

/// <summary>ADR-0022 — approval authority is permission-driven, not a hardcoded role literal.
///
/// <para>Before this change, <c>RequisitionsController</c> and <c>ApprovalChainsController</c>
/// gated every action on <c>Policies.InternalUser</c> / <c>Policies.AdminOnly</c>
/// (<c>RequireRole</c>), so the seeded <c>permission:requisitions:requisitions:*</c> and
/// <c>permission:settings:settings:*</c> codes were decorative — granting or revoking them
/// through the Role Builder changed nothing. These tests prove the codes are now load-bearing:
/// a role that holds the permission can act, a role that does not is stopped at the policy
/// layer (403, before the service is ever reached), and — the actual point of the milestone —
/// a brand new custom role created through the Role Builder and granted only
/// <c>requisitions:approve</c> can decide a real requisition.</para>
/// </summary>
public class RequisitionPermissionAuthorityTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;

    public RequisitionPermissionAuthorityTests(CustomWebAppFactory factory) => _factory = factory;

    private HttpClient ClientFor(string role, Guid? userId = null)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Tenant", _factory.TenantA.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", role);
        if (userId is not null)
            client.DefaultRequestHeaders.Add("X-Test-UserId", userId.Value.ToString());
        return client;
    }

    private async Task<RequisitionDetailDto> NewDraftInSalesAsync(string title)
    {
        var res = await ClientFor(Roles.HiringManager, _factory.HiringManagerUserId)
            .PostAsJsonAsync("/api/requisitions", new CreateRequisitionRequest
            {
                DepartmentId = _factory.SalesDepartmentId,
                Title = title,
                JobDescription = "Because we need one.",
                Headcount = 1,
            });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<RequisitionDetailDto>())!;
    }

    [Fact]
    public async Task HiringManager_Without_Approve_Permission_Gets_403_On_Decision_While_Approver_Succeeds()
    {
        var draft = await NewDraftInSalesAsync("Permission Gate — Decision A");
        await ClientFor(Roles.HiringManager, _factory.HiringManagerUserId)
            .PostAsync($"/api/requisitions/{draft.Id}/submit", null);

        // HiringManager holds requisitions:create/read/update only — the policy layer must
        // stop them before RequisitionService ever runs, so this is 403 (a policy refusal),
        // not the 404 that IsOwnerOrCompanyWide would produce if the service were reached.
        var blocked = await ClientFor(Roles.HiringManager, _factory.HiringManagerUserId)
            .PostAsJsonAsync($"/api/requisitions/{draft.Id}/decision",
                new ApprovalDecisionRequest { Approve = true });
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);

        // Admin (holds approve, and is the named step-1 approver on the seeded chain) can
        // actually decide — proving the permission is not merely a refusal for everyone.
        var step1 = await ClientFor(Roles.Admin, _factory.AdminUserId)
            .PostAsJsonAsync($"/api/requisitions/{draft.Id}/decision",
                new ApprovalDecisionRequest { Approve = true });
        Assert.True(step1.IsSuccessStatusCode, $"Admin should be able to decide; got {step1.StatusCode}.");

        // Approver (holds approve, named step-2 approver) can also decide, and the requisition
        // reaches Approved — the positive case the milestone is actually about.
        var step2 = await ClientFor(Roles.Approver, _factory.FinanceApproverUserId)
            .PostAsJsonAsync($"/api/requisitions/{draft.Id}/decision",
                new ApprovalDecisionRequest { Approve = true });
        step2.EnsureSuccessStatusCode();
        var approved = await step2.Content.ReadFromJsonAsync<RequisitionDetailDto>();
        Assert.Equal("Approved", approved!.Status);
    }

    [Fact]
    public async Task Inbox_Is_Reachable_Only_By_Roles_Holding_Approve_And_Actually_Shows_Their_Work()
    {
        var draft = await NewDraftInSalesAsync("Permission Gate — Inbox A");
        await ClientFor(Roles.HiringManager, _factory.HiringManagerUserId)
            .PostAsync($"/api/requisitions/{draft.Id}/submit", null);

        // Recruiter holds read/create/update but deliberately NOT approve (ADR-0022): raising
        // headcount and approving it are different authorities. So the inbox — which only makes
        // sense for someone who can decide — must stop them at the gate.
        var recruiterInbox = await ClientFor(Roles.Recruiter).GetAsync("/api/requisitions/inbox");
        Assert.Equal(HttpStatusCode.Forbidden, recruiterInbox.StatusCode);

        // HiringManager holds create/read/update — also no approve.
        var managerInbox = await ClientFor(Roles.HiringManager, _factory.HiringManagerUserId)
            .GetAsync("/api/requisitions/inbox");
        Assert.Equal(HttpStatusCode.Forbidden, managerInbox.StatusCode);

        // Admin holds approve and is the named step-1 approver: the gate lets them through,
        // AND the item is genuinely theirs to act on — a positive proof, not just a refusal.
        var adminInbox = await ClientFor(Roles.Admin, _factory.AdminUserId)
            .GetFromJsonAsync<List<RequisitionListItemDto>>("/api/requisitions/inbox");
        Assert.Contains(adminInbox!, r => r.Id == draft.Id);
    }

    [Fact]
    public async Task Recruiter_Can_Raise_And_Submit_A_Requisition_But_Cannot_Approve_It()
    {
        // ADR-0022: recruiters hold requisitions:create and :update, deliberately paired —
        // the flow is create -> edit the draft -> submit, and both the edit and the submit
        // endpoints are gated on :update. Granting create alone would let a recruiter raise
        // something they could then neither correct nor submit.
        var recruiter = ClientFor(Roles.Recruiter, _factory.RecruiterUserId);

        var created = await recruiter.PostAsJsonAsync("/api/requisitions", new CreateRequisitionRequest
        {
            DepartmentId = _factory.SalesDepartmentId,
            Title = "Recruiter-raised opening",
            JobDescription = "Raised by the recruiter, not the hiring manager.",
            Headcount = 1,
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var draft = (await created.Content.ReadFromJsonAsync<RequisitionDetailDto>())!;

        // :update — editing the draft they just raised.
        var edited = await recruiter.PutAsJsonAsync($"/api/requisitions/{draft.Id}", new UpdateRequisitionRequest
        {
            DepartmentId = _factory.SalesDepartmentId,
            Title = "Recruiter-raised opening (revised)",
            JobDescription = "Revised before submitting.",
            Headcount = 2,
        });
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);

        // :update again — submit is gated on the same code, which is why the pair matters.
        var submitted = await recruiter.PostAsync($"/api/requisitions/{draft.Id}/submit", null);
        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);

        // But raising headcount is not authority to approve it — the chain decides that.
        var decided = await recruiter.PostAsJsonAsync($"/api/requisitions/{draft.Id}/decision",
            new ApprovalDecisionRequest { Approve = true, Comment = "Approving my own request." });
        Assert.Equal(HttpStatusCode.Forbidden, decided.StatusCode);
    }

    [Fact]
    public async Task HrDirector_Can_Read_Approval_Chains_But_Cannot_Create_One()
    {
        // HrDirector holds settings:read only (not settings:update). Before M2 this endpoint
        // was Admin-only by role literal, so HrDirector 403'd despite the Sidebar showing the
        // nav item to anyone with settings:read — a real defect this migration also fixes.
        var listRes = await ClientFor(Roles.HrDirector).GetAsync("/api/approvalchains");
        listRes.EnsureSuccessStatusCode();
        var chains = await listRes.Content.ReadFromJsonAsync<List<ApprovalChainDto>>();
        // Positive proof, not just a status code: the seeded default chain is genuinely visible.
        Assert.Contains(chains!, c => c.Name == "Default headcount approval");

        var createRes = await ClientFor(Roles.HrDirector).PostAsJsonAsync("/api/approvalchains",
            new CreateApprovalChainRequest
            {
                Name = "HrDirector attempted chain",
                DepartmentId = _factory.FinanceDepartmentId,
                Steps = new[]
                {
                    new CreateApprovalChainStepRequest { ApproverUserId = _factory.AdminUserId, Label = "HR" },
                },
            });
        Assert.Equal(HttpStatusCode.Forbidden, createRes.StatusCode);
    }

    [Fact]
    public async Task A_Custom_Role_Denied_A_Permission_Is_Actually_Denied_Not_Floored_To_Recruiter()
    {
        // The other direction of the milestone, and the one that was broken. Granting a
        // permission to a custom role always worked; WITHHOLDING one did not.
        //
        // AssignRoleToUserAsync sets user.Role = UserRole.Recruiter for every custom role
        // (UserService.cs:318-325) — a custom role's generated code never parses as a
        // UserRole, so that is the only reachable branch. That literal becomes the JWT role
        // claim. PermissionAuthorizationHandler used to fall through, on a *denied* database
        // check, to matching that claim against the static RbacSeedData list — so every
        // custom-role user silently inherited Recruiter's entire seeded permission set.
        //
        // ADR-0022 added requisitions:create and :update to Recruiter's seed, which turned
        // that latent flaw into "any custom role can create and edit requisitions regardless
        // of what the Role Builder granted it". The fallback was removed; this test is what
        // stops it coming back.
        var admin = ClientFor(Roles.Admin, _factory.AdminUserId);

        var roleRes = await admin.PostAsJsonAsync("/api/roles", new CreateRoleRequest(
            Name: $"Report Viewer {Guid.NewGuid():N}",
            Code: null,
            Description: "Read-only role that must NOT be able to raise requisitions.",
            PermissionCodes: new[] { "permission:requisitions:requisitions:read" }));
        roleRes.EnsureSuccessStatusCode();
        var readOnlyRole = (await roleRes.Content.ReadFromJsonAsync<RoleDetailDto>())!;
        Assert.DoesNotContain("permission:requisitions:requisitions:create", readOnlyRole.AssignedPermissionCodes);

        var userRes = await admin.PostAsJsonAsync("/api/users", new CreateUserRequest(
            Email: $"report.viewer.{Guid.NewGuid():N}@alpha.test",
            DisplayName: "Report Viewer",
            Password: "P@ssw0rd-Test-123",
            RoleId: readOnlyRole.Id,
            Role: null));
        userRes.EnsureSuccessStatusCode();
        var viewer = (await userRes.Content.ReadFromJsonAsync<UserDetailDto>())!;
        Assert.DoesNotContain("permission:requisitions:requisitions:create", viewer.Permissions);

        // The claim really does say "Recruiter" — pinned so this test keeps testing the
        // actual escalation path rather than a hypothetical one.
        Assert.Equal("Recruiter", viewer.Role);

        var client = ClientFor(viewer.Role, viewer.Id);

        var created = await client.PostAsJsonAsync("/api/requisitions", new CreateRequisitionRequest
        {
            DepartmentId = _factory.SalesDepartmentId,
            Title = "Should never be created",
            JobDescription = "The Role Builder withheld requisitions:create.",
            Headcount = 1,
        });
        Assert.Equal(HttpStatusCode.Forbidden, created.StatusCode);

        // Reading is genuinely granted, so this proves the denial above is the permission
        // gate doing its job rather than the user being broken or unauthenticated.
        var listed = await client.GetAsync("/api/requisitions");
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
    }

    [Fact]
    public async Task A_Custom_Role_Granted_Only_Approve_Can_Actually_Decide_A_Requisition()
    {
        // This is the whole point of the milestone: a role invented through the Role Builder
        // — not one of the fixed UserRole literals — reaches the decision endpoint solely
        // because it was granted permission:requisitions:requisitions:approve.
        var admin = ClientFor(Roles.Admin, _factory.AdminUserId);

        var roleRes = await admin.PostAsJsonAsync("/api/roles", new CreateRoleRequest(
            Name: $"Finance Gatekeeper {Guid.NewGuid():N}",
            Code: null,
            Description: "Custom role granted only the approve permission.",
            PermissionCodes: new[] { "permission:requisitions:requisitions:approve" }));
        roleRes.EnsureSuccessStatusCode();
        var customRole = await roleRes.Content.ReadFromJsonAsync<RoleDetailDto>();
        Assert.DoesNotContain("permission:requisitions:requisitions:read", customRole!.AssignedPermissionCodes);

        var userRes = await admin.PostAsJsonAsync("/api/users", new CreateUserRequest(
            Email: $"custom.gatekeeper.{Guid.NewGuid():N}@alpha.test",
            DisplayName: "Custom Gatekeeper",
            Password: "P@ssw0rd-Test-123",
            RoleId: customRole.Id,
            Role: null));
        userRes.EnsureSuccessStatusCode();
        var customUser = await userRes.Content.ReadFromJsonAsync<UserDetailDto>();
        Assert.Contains("permission:requisitions:requisitions:approve", customUser!.Permissions);

        // A department-specific chain whose only step names the new custom-role user.
        var chainRes = await admin.PostAsJsonAsync("/api/approvalchains", new CreateApprovalChainRequest
        {
            Name = $"Finance custom gate {Guid.NewGuid():N}",
            DepartmentId = _factory.FinanceDepartmentId,
            Steps = new[]
            {
                new CreateApprovalChainStepRequest { ApproverUserId = customUser.Id, Label = "Custom Gate" },
            },
        });
        chainRes.EnsureSuccessStatusCode();

        // Finance's own manager raises and submits a requisition into Finance — the
        // department-specific chain above takes priority over the company-wide default.
        var financeManager = ClientFor(Roles.HiringManager, _factory.FinanceManagerUserId);
        var createRes = await financeManager.PostAsJsonAsync("/api/requisitions", new CreateRequisitionRequest
        {
            DepartmentId = _factory.FinanceDepartmentId,
            Title = "Financial Controller",
            JobDescription = "Needs the new custom gate to approve it.",
            Headcount = 1,
        });
        createRes.EnsureSuccessStatusCode();
        var draft = (await createRes.Content.ReadFromJsonAsync<RequisitionDetailDto>())!;

        var submitRes = await financeManager.PostAsync($"/api/requisitions/{draft.Id}/submit", null);
        submitRes.EnsureSuccessStatusCode();
        var submitted = await submitRes.Content.ReadFromJsonAsync<RequisitionDetailDto>();
        Assert.Equal("Custom Gate", submitted!.AwaitingApprovalFrom);

        // The custom-role user — not any of the fixed UserRole literals — decides.
        // HasPermissionAsync resolves purely from the DB user's RoleId, not from this claim.
        // The Role claim itself must still be sent to mirror a real JWT (JwtTokenService
        // always issues one from the legacy User.Role enum, ClaimTypes.Role); since the
        // custom role's generated code doesn't parse to a UserRole, AssignRoleToUserAsync
        // fell back user.Role to Recruiter — asserted below so this test breaks loudly if
        // that fallback ever changes instead of silently sending the wrong claim.
        Assert.Equal("Recruiter", customUser.Role);
        var customClient = _factory.CreateClient();
        customClient.DefaultRequestHeaders.Add("X-Test-Tenant", _factory.TenantA.ToString());
        customClient.DefaultRequestHeaders.Add("X-Test-UserId", customUser.Id.ToString());
        customClient.DefaultRequestHeaders.Add("X-Test-Roles", customUser.Role);

        var decideRes = await customClient.PostAsJsonAsync($"/api/requisitions/{draft.Id}/decision",
            new ApprovalDecisionRequest { Approve = true, Comment = "Granted purely via the Role Builder." });
        Assert.True(decideRes.IsSuccessStatusCode,
            $"A custom role granted requisitions:approve should be able to decide; got {decideRes.StatusCode}.");

        var decided = await decideRes.Content.ReadFromJsonAsync<RequisitionDetailDto>();
        Assert.Equal("Approved", decided!.Status); // single-step chain: one approval closes it
    }

    [Fact]
    public async Task Permission_Gate_Does_Not_Bypass_Department_Scoping_On_Create()
    {
        // The HiringManager owns Sales only, but holds requisitions:create — the coarse
        // permission gate must not let that substitute for CanAccessAsync's department check.
        // Unchanged service behaviour; this is a regression guard for the controller edit.
        var res = await ClientFor(Roles.HiringManager, _factory.HiringManagerUserId)
            .PostAsJsonAsync("/api/requisitions", new CreateRequisitionRequest
            {
                DepartmentId = _factory.FinanceDepartmentId,
                Title = "Should Not Be Creatable",
                JobDescription = "Wrong department for this requester.",
                Headcount = 1,
            });

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode); // ADR-0003: 404, not 403
    }
}
