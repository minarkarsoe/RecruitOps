using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RecruitOps.Api.Auth;
using RecruitOps.Application.Common.Exceptions;
using RecruitOps.Application.DTOs.Ai;
using RecruitOps.Infrastructure.Options;
using RecruitOps.Infrastructure.Services;
using RecruitOps.Infrastructure.Services.MyanmarScript;
using Xunit;

namespace RecruitOps.Api.Tests;

public class AiStressAndResilienceTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;

    public AiStressAndResilienceTests(CustomWebAppFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateAuthorizedClient(string role = Roles.Recruiter)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Tenant", _factory.TenantA.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", role);
        return client;
    }

    #region 1. Validation and Invalid Payload Stress Tests (400 Bad Request)

    [Theory]
    [InlineData("/api/ai/parse-resume")]
    [InlineData("/api/ai/claude/parse-resume")]
    public async Task ParseResume_Returns_400_When_ResumeText_Is_Empty(string endpoint)
    {
        var client = CreateAuthorizedClient();
        var request = new ParseResumeRequest("", "file.pdf");

        var response = await client.PostAsJsonAsync(endpoint, request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Invalid Request Payload", problem.Title);
    }

    [Theory]
    [InlineData("/api/ai/match-candidate")]
    [InlineData("/api/ai/claude/match-candidate")]
    public async Task MatchCandidate_Returns_400_When_CandidateId_Or_JobPostingId_Is_Empty(string endpoint)
    {
        var client = CreateAuthorizedClient();
        var request = new MatchCandidateRequest(Guid.Empty, Guid.NewGuid());

        var response = await client.PostAsJsonAsync(endpoint, request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/ai/executive-summary")]
    [InlineData("/api/ai/gemini/executive-summary")]
    public async Task GenerateExecutiveSummary_Returns_400_When_CandidateId_Is_Empty(string endpoint)
    {
        var client = CreateAuthorizedClient();
        var request = new GenerateExecutiveSummaryRequest(Guid.Empty);

        var response = await client.PostAsJsonAsync(endpoint, request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/ai/document-prep")]
    [InlineData("/api/ai/gemini/document-prep")]
    public async Task PrepareDocument_Returns_400_When_DocumentType_Is_Empty(string endpoint)
    {
        var client = CreateAuthorizedClient();
        var request = new PrepareDocumentRequest(Guid.NewGuid(), Guid.NewGuid(), "");

        var response = await client.PostAsJsonAsync(endpoint, request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/ai/translate")]
    [InlineData("/api/ai/gemini/translate")]
    [InlineData("/api/ai/gemini/burmese-localization")]
    public async Task Translate_Returns_400_When_SourceText_Is_Empty(string endpoint)
    {
        var client = CreateAuthorizedClient();
        var request = new BurmeseLocalizationRequest("", "en");

        var response = await client.PostAsJsonAsync(endpoint, request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region 2. API Key Error & External Network Fault Stress Tests (No 500 Crashes)

    // These tests used to assert the opposite — that a 401 or a 500 from the provider still produced
    // "Aung Kyaw Thu" and a match score. A configured key means someone is relying on the answer, so
    // an unusable provider response must surface as a failure, never as a plausible-looking one.

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)] // 401 Bad API Key
    [InlineData(HttpStatusCode.Forbidden)]    // 403 Forbidden Key
    [InlineData(HttpStatusCode.TooManyRequests)] // 429 Rate Limit Exceeded
    [InlineData(HttpStatusCode.InternalServerError)] // 500 LLM Provider Down
    public async Task ClaudeApiClient_Surfaces_Http_Errors_Instead_Of_Fabricating(HttpStatusCode providerStatus)
    {
        var handler = new MockFaultyHttpMessageHandler(providerStatus, "{\"error\": {\"message\": \"API Key Invalid\"}}");
        var httpClient = new HttpClient(handler);
        var options = Options.Create(new ClaudeOptions { ApiKey = "sk-ant-invalid-key-999" });
        var normalizer = new MyanmarScriptNormalizer();
        var client = new ClaudeApiClient(httpClient, options, NullLogger<ClaudeApiClient>.Instance, normalizer);

        var parseFailure = await Assert.ThrowsAsync<AiProviderUnavailableException>(
            () => client.ParseResumeAsync(new ParseResumeRequest("CV text")));
        await Assert.ThrowsAsync<AiProviderUnavailableException>(
            () => client.MatchCandidateAsync(new MatchCandidateRequest(Guid.NewGuid(), Guid.NewGuid())));

        Assert.Equal("Claude", parseFailure.ProviderName);
        Assert.Contains(((int)providerStatus).ToString(), parseFailure.Message);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task GeminiApiClient_Surfaces_Http_Errors_Instead_Of_Fabricating(HttpStatusCode providerStatus)
    {
        var handler = new MockFaultyHttpMessageHandler(providerStatus, "{\"error\": {\"code\": 401, \"message\": \"API Key Invalid\"}}");
        var httpClient = new HttpClient(handler);
        var options = Options.Create(new GeminiOptions { ApiKey = "AIzaSyInvalidKey999" });
        var normalizer = new MyanmarScriptNormalizer();
        var client = new GeminiApiClient(httpClient, options, NullLogger<GeminiApiClient>.Instance, normalizer);

        var summaryFailure = await Assert.ThrowsAsync<AiProviderUnavailableException>(
            () => client.GenerateExecutiveSummaryAsync(new GenerateExecutiveSummaryRequest(Guid.NewGuid())));
        await Assert.ThrowsAsync<AiProviderUnavailableException>(
            () => client.PrepareDocumentAsync(new PrepareDocumentRequest(Guid.NewGuid(), Guid.NewGuid(), "InterviewKit")));
        await Assert.ThrowsAsync<AiProviderUnavailableException>(
            () => client.TranslateBurmeseAsync(new BurmeseLocalizationRequest("Hello", "my")));

        Assert.Equal("Gemini", summaryFailure.ProviderName);
        Assert.Contains(((int)providerStatus).ToString(), summaryFailure.Message);
    }

    [Fact]
    public async Task ClaudeApiClient_Surfaces_Malformed_Json_Instead_Of_Fabricating()
    {
        var handler = new MockFaultyHttpMessageHandler(HttpStatusCode.OK, "CORRUPTED_NON_JSON_RESPONSE{{{");
        var httpClient = new HttpClient(handler);
        var options = Options.Create(new ClaudeOptions { ApiKey = "sk-ant-test-key" });
        var normalizer = new MyanmarScriptNormalizer();
        var client = new ClaudeApiClient(httpClient, options, NullLogger<ClaudeApiClient>.Instance, normalizer);

        var failure = await Assert.ThrowsAsync<AiProviderUnavailableException>(
            () => client.ParseResumeAsync(new ParseResumeRequest("CV text")));

        Assert.DoesNotContain("Aung Kyaw Thu", failure.Message);
    }

    [Fact]
    public async Task GeminiApiClient_Surfaces_Malformed_Json_Instead_Of_Fabricating()
    {
        var handler = new MockFaultyHttpMessageHandler(HttpStatusCode.OK, "CORRUPTED_NON_JSON_RESPONSE{{{");
        var httpClient = new HttpClient(handler);
        var options = Options.Create(new GeminiOptions { ApiKey = "AIzaSyTestKey" });
        var normalizer = new MyanmarScriptNormalizer();
        var client = new GeminiApiClient(httpClient, options, NullLogger<GeminiApiClient>.Instance, normalizer);

        await Assert.ThrowsAsync<AiProviderUnavailableException>(
            () => client.GenerateExecutiveSummaryAsync(new GenerateExecutiveSummaryRequest(Guid.NewGuid())));
    }

    /// <summary>
    /// A 200 whose body is well-formed JSON but not the expected shape — the case that used to slip
    /// through <c>JsonDocument.Parse</c> and land on the stub without so much as a warning.
    /// </summary>
    [Fact]
    public async Task ClaudeApiClient_Surfaces_Unexpected_Response_Shape_Instead_Of_Fabricating()
    {
        var handler = new MockFaultyHttpMessageHandler(HttpStatusCode.OK, "{\"unexpected\": \"shape\"}");
        var httpClient = new HttpClient(handler);
        var options = Options.Create(new ClaudeOptions { ApiKey = "sk-ant-test-key" });
        var normalizer = new MyanmarScriptNormalizer();
        var client = new ClaudeApiClient(httpClient, options, NullLogger<ClaudeApiClient>.Instance, normalizer);

        await Assert.ThrowsAsync<AiProviderUnavailableException>(
            () => client.ParseResumeAsync(new ParseResumeRequest("CV text")));
    }

    #endregion

    #region 3. Output Integrity & Criteria Breakdown Validation

    [Fact]
    public async Task MatchCandidate_Criteria_Breakdown_Integrity_Check()
    {
        var client = CreateAuthorizedClient();
        var request = new MatchCandidateRequest(Guid.NewGuid(), Guid.NewGuid());

        var response = await client.PostAsJsonAsync("/api/ai/match-candidate", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<CandidateMatchAnalysisDto>();
        Assert.NotNull(dto);

        // Verify scoring calculation is strictly bounded
        Assert.InRange(dto.MatchScore, 0, 100);

        // Verify criteria breakdown integrity
        Assert.NotNull(dto.MatchedSkills);
        Assert.NotNull(dto.MissingSkills);
        Assert.NotNull(dto.Strengths);
        Assert.NotNull(dto.Concerns);

        Assert.NotEmpty(dto.OverallVerdict);
        Assert.NotEmpty(dto.Recommendation);
    }

    [Fact]
    public async Task DocumentPrep_Generated_Document_Formats_Are_NonEmpty_And_Valid()
    {
        var client = CreateAuthorizedClient();
        var request = new PrepareDocumentRequest(Guid.NewGuid(), Guid.NewGuid(), "ClientDossier");

        var response = await client.PostAsJsonAsync("/api/ai/document-prep", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<DocumentPrepResultDto>();
        Assert.NotNull(dto);

        Assert.False(string.IsNullOrWhiteSpace(dto.DocumentTitle));
        Assert.Contains("# ", dto.ContentMarkdown); // Contains markdown header
        Assert.Contains("<div", dto.ContentHtml);  // Contains HTML elements
    }

    #endregion

    private class MockFaultyHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _responseContent;

        public MockFaultyHttpMessageHandler(HttpStatusCode statusCode, string responseContent)
        {
            _statusCode = statusCode;
            _responseContent = responseContent;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseContent, System.Text.Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
