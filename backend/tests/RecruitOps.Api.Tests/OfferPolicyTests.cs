using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RecruitOps.Api.Auth;
using RecruitOps.Application.DTOs;
using RecruitOps.Domain.Enums;
using RecruitOps.Infrastructure.Persistence;
using Xunit;

namespace RecruitOps.Api.Tests;

/// <summary>Offers with no offer chain configured. Its own class — and so its own database — because
/// <see cref="OfferTests"/> creates one, and chains are company-wide.</summary>
public class OfferWithoutChainTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;
    private readonly Module3Scenario _scenario;

    public OfferWithoutChainTests(CustomWebAppFactory factory)
    {
        _factory = factory;
        _scenario = new Module3Scenario(factory);
    }

    [Fact]
    public async Task Without_An_Offer_Chain_Approval_Is_Refused_With_The_Reason_Not_Guessed()
    {
        // The fixture has requisition chains only. They must not be borrowed for an offer.
        var recruiter = _scenario.Client(Roles.Recruiter, _factory.RecruiterUserId);
        var draft = await OfferTests.DraftAsync(_scenario, recruiter, "Offer No Chain");
        var over = await OfferTests.TermsAsync(recruiter, draft, 900_000m);
        Assert.True(over.NeedsApproval);
        Assert.Null(over.ApprovalPreview); // the editor says "no offer chain" from this

        var res = await recruiter.PostAsync($"/api/offers/{draft.Id}/submit", null);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Contains("No offer approval chain", await res.Content.ReadAsStringAsync());

        var still = await recruiter.GetFromJsonAsync<OfferDetailDto>($"/api/offers/{draft.Id}");
        Assert.Equal("Draft", still!.Status);
        Assert.Empty(still.Approvals);
    }
}

/// <summary>The company's offer policy (ADR-0030 §2, §4). Its own database: the settings are
/// company-wide and would change every other offer test's answers.</summary>
public class OfferSettingsTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;
    private readonly Module3Scenario _scenario;

    public OfferSettingsTests(CustomWebAppFactory factory)
    {
        _factory = factory;
        _scenario = new Module3Scenario(factory);
    }

    private HttpClient Admin() => _scenario.Client(Roles.Admin, _factory.AdminUserId);
    private HttpClient Recruiter() => _scenario.Client(Roles.Recruiter, _factory.RecruiterUserId);

    private static UpdateOfferSettingsRequest Settings(bool requireApproval, bool showSalary) =>
        new() { RequireOfferApproval = requireApproval, ShowOfferSalaryToHiringManagers = showSalary };

    [Fact]
    public async Task The_Policy_Is_Read_And_Changed_Only_With_Settings_Permission_And_Company_Reach()
    {
        Assert.Equal(new OfferSettingsDto(false, false), await Admin().GetFromJsonAsync<OfferSettingsDto>("/api/settings/offers"));

        // A real Recruiter holds no settings:update; a Hiring Manager has no company-wide reach.
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Recruiter().PutAsJsonAsync("/api/settings/offers", new UpdateOfferSettingsRequest { RequireOfferApproval = true, ShowOfferSalaryToHiringManagers = true })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await _scenario.SalesManager().PutAsJsonAsync("/api/settings/offers", new UpdateOfferSettingsRequest { RequireOfferApproval = true, ShowOfferSalaryToHiringManagers = true })).StatusCode);
        Assert.Equal(new OfferSettingsDto(false, false), await Admin().GetFromJsonAsync<OfferSettingsDto>("/api/settings/offers"));
    }

    [Fact]
    public async Task Requiring_Approval_For_Every_Offer_Routes_One_Within_Budget()
    {
        await OfferTests.EnsureOfferChainAsync(Admin(), _factory.SalesDepartmentId, _factory.FinanceApproverUserId);
        var draft = await OfferTests.DraftAsync(_scenario, Recruiter(), "Offer Always Approve");
        Assert.False(draft.OverBudget);

        try
        {
            (await Admin().PutAsJsonAsync("/api/settings/offers", Settings(true, false))).EnsureSuccessStatusCode();

            var now = await Recruiter().GetFromJsonAsync<OfferDetailDto>($"/api/offers/{draft.Id}");
            Assert.True(now!.NeedsApproval);
            Assert.True(now.PolicyRequiresApproval);
            var res = await Recruiter().PostAsync($"/api/offers/{draft.Id}/submit", null);
            res.EnsureSuccessStatusCode();
            Assert.Equal("PendingApproval", (await res.Content.ReadFromJsonAsync<OfferDetailDto>())!.Status);
        }
        finally
        {
            (await Admin().PutAsJsonAsync("/api/settings/offers", Settings(false, false))).EnsureSuccessStatusCode();
        }
    }

    [Fact]
    public async Task Showing_Salary_To_Hiring_Managers_Is_The_Companys_Choice()
    {
        var draft = await OfferTests.DraftAsync(_scenario, Recruiter(), "Offer Show Salary");
        Assert.Null((await _scenario.SalesManager().GetFromJsonAsync<OfferDetailDto>($"/api/offers/{draft.Id}"))!.BaseSalary);

        try
        {
            (await Admin().PutAsJsonAsync("/api/settings/offers", Settings(false, true))).EnsureSuccessStatusCode();

            var shown = await _scenario.SalesManager().GetFromJsonAsync<OfferDetailDto>($"/api/offers/{draft.Id}");
            Assert.True(shown!.SalaryVisible);
            Assert.Equal(800_000m, shown.BaseSalary);
            var listed = (await _scenario.SalesManager().GetFromJsonAsync<List<OfferListItemDto>>("/api/offers"))!
                .Single(o => o.Id == draft.Id);
            Assert.Equal(800_000m, listed.BaseSalary);
            Assert.False(shown.CanManage); // still read-only
        }
        finally
        {
            (await Admin().PutAsJsonAsync("/api/settings/offers", Settings(false, false))).EnsureSuccessStatusCode();
        }
    }

    [Fact]
    public async Task Changing_The_Policy_Is_Answerable_And_Half_A_Policy_Is_Refused()
    {
        // An omitted field must not silently become false and switch a control off (security review, L4).
        var half = await Admin().PutAsJsonAsync("/api/settings/offers", new { requireOfferApproval = true });
        Assert.Equal(HttpStatusCode.BadRequest, half.StatusCode);

        try
        {
            (await Admin().PutAsJsonAsync("/api/settings/offers", Settings(false, true))).EnsureSuccessStatusCode();

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var entry = await db.AuditLogEntries.IgnoreQueryFilters()
                .SingleAsync(e => e.Action == AuditAction.OfferSettingsChanged && e.Summary.Contains("now see offered salaries"));
            Assert.Equal(_factory.AdminUserId, entry.ActorUserId);
        }
        finally
        {
            (await Admin().PutAsJsonAsync("/api/settings/offers", Settings(false, false))).EnsureSuccessStatusCode();
        }
    }
}
