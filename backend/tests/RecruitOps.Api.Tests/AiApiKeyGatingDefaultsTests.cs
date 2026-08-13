using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using RecruitOps.Api.Auth;
using Xunit;

namespace RecruitOps.Api.Tests;

/// <summary>
/// The AI endpoints under the configuration a customer install actually ships with: no API key and
/// <c>AI:*:EnableFallback = false</c>.
/// </summary>
/// <remarks>
/// This class exists because the rest of the AI suite runs with the development fallback switched
/// on, and a suite that only ever exercises the stub path will stay green no matter what the
/// unconfigured default does. ADR-0008 makes AI optional and gated; "optional" has to mean the
/// feature reports itself off, not that it invents a candidate.
/// </remarks>
public class AiApiKeyGatingDefaultsTests : IClassFixture<NoAiFallbackWebAppFactory>
{
    private readonly NoAiFallbackWebAppFactory _factory;

    public AiApiKeyGatingDefaultsTests(NoAiFallbackWebAppFactory factory)
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

    [Theory]
    [InlineData("/api/ai/parse-resume")]
    [InlineData("/api/ai/claude/parse-resume")]
    [InlineData("/api/ai/match-candidate")]
    [InlineData("/api/ai/claude/match-candidate")]
    [InlineData("/api/ai/executive-summary")]
    [InlineData("/api/ai/gemini/executive-summary")]
    [InlineData("/api/ai/document-prep")]
    [InlineData("/api/ai/gemini/document-prep")]
    [InlineData("/api/ai/translate")]
    [InlineData("/api/ai/gemini/burmese-localization")]
    public async Task Unconfigured_ApiKey_Returns_402_On_The_Shipped_Default(string endpoint)
    {
        var client = CreateAuthorizedClient();

        var response = await client.PostAsJsonAsync(
            endpoint, AiProviderIntegrationAndGatingTests.PayloadFor(endpoint));

        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(402, problem.Status);
        Assert.Equal("AI Feature Disabled or API Key Unconfigured", problem.Title);
        Assert.Equal("https://recruitops.io/errors/ai-feature-disabled", problem.Type);
    }

    /// <summary>
    /// The failure this whole change is about: a 200 carrying a fabricated profile or match score.
    /// Asserting "not 200" is the point — a 402 body is checked above, and any future third outcome
    /// is still not permitted to be a plausible-looking answer.
    /// </summary>
    [Theory]
    [InlineData("/api/ai/parse-resume")]
    [InlineData("/api/ai/match-candidate")]
    [InlineData("/api/ai/executive-summary")]
    [InlineData("/api/ai/document-prep")]
    [InlineData("/api/ai/translate")]
    public async Task Unconfigured_ApiKey_Never_Returns_Fabricated_Content(string endpoint)
    {
        var client = CreateAuthorizedClient();

        var response = await client.PostAsJsonAsync(
            endpoint, AiProviderIntegrationAndGatingTests.PayloadFor(endpoint));

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Aung Kyaw Thu", body);
        Assert.DoesNotContain("Strong Fit", body);
    }
}
