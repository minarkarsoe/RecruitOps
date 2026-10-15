using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using RecruitOps.Api.Auth;
using RecruitOps.Infrastructure.Persistence;
using Xunit;

namespace RecruitOps.Api.Tests;

/// <summary>Every role the API admits to an action holds the permission the UI gates that action on.
/// <para>The hiring endpoints are guarded by <b>role policies</b>; the UI gates their buttons on
/// <b>permissions</b> from the seed matrices. The two were written separately and never compared:
/// while the client's <c>hasPermission</c> failed open nobody could tell, and once it failed closed
/// (2026-10-15) a Dept-Head approver lost the Approve button, an Approver on a panel lost Submit,
/// a Hiring Manager could no longer cancel their own requisition, and a Recruiter lost the
/// scorecard-template editor — every one of them allowed by the API.</para>
/// <para>The policies are read from the running app, not restated here, so a policy change is
/// followed automatically. Only the permission → policy pairing is written down, and it is the
/// pairing the UI makes. The reverse direction (a permission the API would refuse) is not pinned:
/// a few seed grants are broader than their endpoints — recorded in FEATURE-STATUS — and none of
/// these codes guards an endpoint, so a broad grant shows a button that the API then refuses.</para></summary>
public class SeedMatrixMirrorsPoliciesTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;

    public SeedMatrixMirrorsPoliciesTests(CustomWebAppFactory factory) => _factory = factory;

    [Theory]
    // Postings — JobPostingsController, RecruitmentStaff per action.
    [InlineData("permission:postings:postings:create", Policies.RecruitmentStaff)]
    [InlineData("permission:postings:postings:update", Policies.RecruitmentStaff)]
    [InlineData("permission:postings:postings:publish", Policies.RecruitmentStaff)]
    // Pipeline — ApplicationsController stage move.
    [InlineData("permission:applications:applications:move_stage", Policies.RecruitmentStaff)]
    // Scorecard templates — ScorecardTemplatesController POST/PUT.
    [InlineData("permission:scorecards:scorecards:manage_templates", Policies.RecruitmentStaff)]
    // Requisitions — InternalUser on the controller; ownership, department access and "is the
    // active approver" are decided per row by the service and mirrored by the page itself.
    [InlineData("permission:requisitions:requisitions:read", Policies.InternalUser)]
    [InlineData("permission:requisitions:requisitions:create", Policies.InternalUser)]
    [InlineData("permission:requisitions:requisitions:update", Policies.InternalUser)]
    [InlineData("permission:requisitions:requisitions:delete", Policies.InternalUser)]
    [InlineData("permission:requisitions:requisitions:approve", Policies.InternalUser)]
    // Scorecards — InternalUser on the controller; panel membership decides per round. An
    // Approver reaches an application only this way (ADR-0018), so they must be able to submit.
    [InlineData("permission:scorecards:scorecards:submit", Policies.InternalUser)]
    public async Task Every_Role_A_Policy_Admits_Holds_The_Permission_That_Gates_It(string permission, string policy)
    {
        var provider = _factory.Services.GetRequiredService<IAuthorizationPolicyProvider>();
        var admitted = (await provider.GetPolicyAsync(policy))!
            .Requirements.OfType<RolesAuthorizationRequirement>().Single().AllowedRoles;

        var seeds = RbacSeedData.GetSystemRoles();
        var missing = admitted
            .Where(role => !seeds.Single(s => s.Code == role).PermissionCodes.Contains(permission))
            .ToList();

        Assert.True(missing.Count == 0,
            $"{policy} admits {string.Join(", ", missing)} to the action gated on {permission}, " +
            "but their seed matrix lacks it — the UI would hide an action the API allows.");
    }
}
