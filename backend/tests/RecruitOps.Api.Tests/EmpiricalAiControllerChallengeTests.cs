using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using RecruitOps.Api.Auth;
using RecruitOps.Application.DTOs.Ai;
using Xunit;

namespace RecruitOps.Api.Tests;

public class EmpiricalAiControllerChallengeTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;

    public EmpiricalAiControllerChallengeTests(CustomWebAppFactory factory)
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

    #region Scope 1: Fine-Grained RBAC Permission Isolation Stress Tests

    [Fact]
    public async Task Recruiter_Role_Has_Full_Access_To_All_Ai_Endpoints()
    {
        var client = CreateClientForTenant(_factory.TenantA, Roles.Recruiter);

        // 1. Parse Resume
        var parseRes = await client.PostAsJsonAsync("/api/ai/claude/parse-resume", new ParseResumeRequest("Sample resume text", "cv.pdf"));
        Assert.Equal(HttpStatusCode.OK, parseRes.StatusCode);

        // 2. Match Candidate
        var matchRes = await client.PostAsJsonAsync("/api/ai/claude/match-candidate", new MatchCandidateRequest(Guid.NewGuid(), Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.OK, matchRes.StatusCode);

        // 3. Executive Summary
        var summaryRes = await client.PostAsJsonAsync("/api/ai/gemini/executive-summary", new GenerateExecutiveSummaryRequest(Guid.NewGuid(), null, "Exec"));
        Assert.Equal(HttpStatusCode.OK, summaryRes.StatusCode);

        // 4. Document Prep
        var docRes = await client.PostAsJsonAsync("/api/ai/gemini/document-prep", new PrepareDocumentRequest(Guid.NewGuid(), Guid.NewGuid(), "Dossier"));
        Assert.Equal(HttpStatusCode.OK, docRes.StatusCode);

        // 5. Burmese Localization
        var locRes = await client.PostAsJsonAsync("/api/ai/gemini/burmese-localization", new BurmeseLocalizationRequest("Hello", "my", "Greeting"));
        Assert.Equal(HttpStatusCode.OK, locRes.StatusCode);
    }

    [Fact]
    public async Task HiringManager_Role_Is_Blocked_From_All_Ai_Endpoints_With_403()
    {
        var client = CreateClientForTenant(_factory.TenantA, Roles.HiringManager);

        var parseRes = await client.PostAsJsonAsync("/api/ai/claude/parse-resume", new ParseResumeRequest("Sample", "cv.pdf"));
        Assert.Equal(HttpStatusCode.Forbidden, parseRes.StatusCode);

        var matchRes = await client.PostAsJsonAsync("/api/ai/claude/match-candidate", new MatchCandidateRequest(Guid.NewGuid(), Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Forbidden, matchRes.StatusCode);

        var summaryRes = await client.PostAsJsonAsync("/api/ai/gemini/executive-summary", new GenerateExecutiveSummaryRequest(Guid.NewGuid(), null, "Exec"));
        Assert.Equal(HttpStatusCode.Forbidden, summaryRes.StatusCode);

        var docRes = await client.PostAsJsonAsync("/api/ai/gemini/document-prep", new PrepareDocumentRequest(Guid.NewGuid(), Guid.NewGuid(), "Dossier"));
        Assert.Equal(HttpStatusCode.Forbidden, docRes.StatusCode);

        var locRes = await client.PostAsJsonAsync("/api/ai/gemini/burmese-localization", new BurmeseLocalizationRequest("Hello", "my", null));
        Assert.Equal(HttpStatusCode.Forbidden, locRes.StatusCode);
    }

    [Fact]
    public async Task SuperAdmin_Role_Bypasses_Permission_Checks_For_All_Ai_Endpoints()
    {
        // SuperAdmin with no specific role assigned
        var client = CreateClientForTenant(_factory.TenantA, "CustomNoRole", Guid.NewGuid(), isSuperAdmin: true);

        var parseRes = await client.PostAsJsonAsync("/api/ai/claude/parse-resume", new ParseResumeRequest("Sample resume", "cv.pdf"));
        Assert.Equal(HttpStatusCode.OK, parseRes.StatusCode);

        var matchRes = await client.PostAsJsonAsync("/api/ai/claude/match-candidate", new MatchCandidateRequest(Guid.NewGuid(), Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.OK, matchRes.StatusCode);

        var locRes = await client.PostAsJsonAsync("/api/ai/gemini/burmese-localization", new BurmeseLocalizationRequest("Hello", "my", null));
        Assert.Equal(HttpStatusCode.OK, locRes.StatusCode);
    }

    #endregion

    #region Scope 2: Input Validation & Boundary Stress Tests

    [Fact]
    public async Task ParseResume_Returns_400_When_ResumeText_Is_Whitespace_Or_Null()
    {
        var client = CreateClientForTenant(_factory.TenantA, Roles.Recruiter);

        // Whitespace only
        var res1 = await client.PostAsJsonAsync("/api/ai/claude/parse-resume", new ParseResumeRequest("   \n\t   ", "cv.pdf"));
        Assert.Equal(HttpStatusCode.BadRequest, res1.StatusCode);
        var problem1 = await res1.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem1);
        Assert.Equal("ResumeText cannot be empty.", problem1.Detail);

        // Empty string
        var res2 = await client.PostAsJsonAsync("/api/ai/claude/parse-resume", new ParseResumeRequest("", null));
        Assert.Equal(HttpStatusCode.BadRequest, res2.StatusCode);
    }

    [Fact]
    public async Task ExecutiveSummary_Returns_400_When_CandidateId_Is_Empty_Guid()
    {
        var client = CreateClientForTenant(_factory.TenantA, Roles.Recruiter);

        var res = await client.PostAsJsonAsync("/api/ai/gemini/executive-summary", new GenerateExecutiveSummaryRequest(Guid.Empty, Guid.NewGuid(), "Style"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var problem = await res.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("CandidateId must be a valid non-empty GUID.", problem.Detail);
    }

    [Fact]
    public async Task DocumentPrep_Returns_400_When_DocumentType_Is_Missing()
    {
        var client = CreateClientForTenant(_factory.TenantA, Roles.Recruiter);

        // Null DocumentType
        var res1 = await client.PostAsJsonAsync("/api/ai/gemini/document-prep", new PrepareDocumentRequest(Guid.NewGuid(), Guid.NewGuid(), ""));
        Assert.Equal(HttpStatusCode.BadRequest, res1.StatusCode);
        var problem1 = await res1.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem1);
        Assert.Equal("CandidateId, JobPostingId, and DocumentType are required.", problem1.Detail);
    }

    [Fact]
    public async Task BurmeseLocalization_Returns_400_When_TargetLanguage_Is_Whitespace()
    {
        var client = CreateClientForTenant(_factory.TenantA, Roles.Recruiter);

        var res = await client.PostAsJsonAsync("/api/ai/gemini/burmese-localization", new BurmeseLocalizationRequest("Some text", "   ", null));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var problem = await res.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("SourceText and TargetLanguage are required.", problem.Detail);
    }

    #endregion

    #region Scope 3: Unicode & Heavy Payload Resilience Tests

    [Fact]
    public async Task BurmeseLocalization_Handles_Mixed_English_And_Burmese_Text()
    {
        var client = CreateClientForTenant(_factory.TenantA, Roles.Recruiter);
        var burmeseInput = "မင်္ဂလာပါ! Senior Developer with 5 years experience (ရန်ကုန်).";

        var res = await client.PostAsJsonAsync("/api/ai/gemini/burmese-localization", new BurmeseLocalizationRequest(burmeseInput, "my", "CV Note"));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var result = await res.Content.ReadFromJsonAsync<BurmeseLocalizationResultDto>();
        Assert.NotNull(result);
        Assert.Equal("my", result.TargetLanguage);
        Assert.False(string.IsNullOrWhiteSpace(result.TranslatedText));
    }

    [Fact]
    public async Task ParseResume_Handles_Large_Resume_Payload()
    {
        var client = CreateClientForTenant(_factory.TenantA, Roles.Recruiter);
        var largeResume = new string('A', 50000); // 50KB text

        var res = await client.PostAsJsonAsync("/api/ai/claude/parse-resume", new ParseResumeRequest(largeResume, "big_cv.txt"));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var result = await res.Content.ReadFromJsonAsync<ParsedResumeResultDto>();
        Assert.NotNull(result);
    }

    #endregion
}
