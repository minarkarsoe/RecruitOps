using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RecruitOps.Application.Common.Exceptions;
using RecruitOps.Application.DTOs.Ai;
using RecruitOps.Application.Interfaces;
using RecruitOps.Infrastructure.Options;

namespace RecruitOps.Infrastructure.Services;

public class GeminiApiClient : IGeminiService
{
    private const string ProviderName = "Gemini";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiApiClient> _logger;
    private readonly IMyanmarScriptNormalizer _normalizer;
    private readonly IAiSimulationScope? _simulation;

    public GeminiApiClient(
        HttpClient httpClient,
        IOptions<GeminiOptions> options,
        ILogger<GeminiApiClient> logger,
        IMyanmarScriptNormalizer normalizer,
        IAiSimulationScope? simulation = null)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _normalizer = normalizer;
        _simulation = simulation;
    }

    /// <summary>
    /// Decides, once, what an unconfigured key means — see
    /// <see cref="ClaudeApiClient"/> for the reasoning. Returns true when the caller should serve a
    /// development stub; throws <see cref="AiApiKeyMissingException"/> (402) when it should not.
    /// </summary>
    private bool ShouldServeDevelopmentStub()
    {
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return false;
        }

        if (!_options.EnableFallback)
        {
            throw new AiApiKeyMissingException(ProviderName);
        }

        _logger.LogWarning(
            "Gemini API key is not configured and AI:Gemini:EnableFallback is on — returning a fabricated sample. "
            + "Responses are stamped X-Ai-Simulated: true. This must not be enabled outside local development.");
        _simulation?.MarkSimulated(ProviderName);
        return true;
    }

    public async Task<ExecutiveSummaryDto> GenerateExecutiveSummaryAsync(
        GenerateExecutiveSummaryRequest request, string? candidateProfileData = null, string? jobPostingData = null, CancellationToken ct = default)
    {
        if (ShouldServeDevelopmentStub())
        {
            return GetExecutiveSummaryStub(request);
        }

        var completion = await RequestCompletionAsync(
            $"Generate an executive summary for Candidate {request.CandidateId}. "
            + $"Candidate profile: {candidateProfileData}\nJob posting: {jobPostingData}", ct);

        return DeserializeCompletion<ExecutiveSummaryDto>(completion);
    }

    public async Task<DocumentPrepResultDto> PrepareDocumentAsync(
        PrepareDocumentRequest request, string? candidateProfileData = null, string? jobPostingData = null, CancellationToken ct = default)
    {
        if (ShouldServeDevelopmentStub())
        {
            return GetDocumentPrepStub(request);
        }

        var completion = await RequestCompletionAsync(
            $"Generate a {request.DocumentType} document for Candidate {request.CandidateId}. "
            + $"Candidate profile: {candidateProfileData}\nJob posting: {jobPostingData}", ct);

        return DeserializeCompletion<DocumentPrepResultDto>(completion);
    }

    public async Task<BurmeseLocalizationResultDto> TranslateBurmeseAsync(
        BurmeseLocalizationRequest request, CancellationToken ct = default)
    {
        // Zawgyi to Unicode normalization per ADR-0009, before anything is sent or stubbed.
        var normalizedInput = _normalizer.Normalize(request.SourceText).NormalizedText;

        if (ShouldServeDevelopmentStub())
        {
            return GetBurmeseLocalizationStub(new BurmeseLocalizationRequest(normalizedInput, request.TargetLanguage, request.Context));
        }

        var completion = await RequestCompletionAsync(
            $"Translate the following text to {request.TargetLanguage}:\n{normalizedInput}", ct);

        // Translation is the one call whose completion is prose, not JSON.
        return new BurmeseLocalizationResultDto(
            OriginalText: normalizedInput,
            TranslatedText: completion.Trim(),
            SourceLanguage: request.TargetLanguage == "my" ? "en" : "my",
            TargetLanguage: request.TargetLanguage
        );
    }

    /// <summary>
    /// Calls Gemini and returns the completion text. Any outcome that is not a usable completion
    /// — transport fault, timeout, non-success status, unexpected body — raises
    /// <see cref="AiProviderUnavailableException"/>, which the API turns into 502.
    /// </summary>
    private async Task<string> RequestCompletionAsync(string prompt, CancellationToken ct)
    {
        var url = $"{_options.ApiUrl}/{_options.Model}:generateContent?key={_options.ApiKey}";
        var payload = new
        {
            contents = new[] { new { parts = new[] { new { text = prompt } } } }
        };

        HttpResponseMessage resp;
        try
        {
            resp = await _httpClient.PostAsJsonAsync(url, payload, ct);
        }
        // A caller who went away is a cancellation, not a provider fault; a timeout is the reverse.
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogError(ex, "Gemini API call timed out after {TimeoutSeconds}s.", _options.TimeoutSeconds);
            throw new AiProviderUnavailableException(ProviderName, "the request timed out", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gemini API call failed before a response was received.");
            throw new AiProviderUnavailableException(ProviderName, "the request failed before a response was received", ex);
        }

        using (resp)
        {
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogError("Gemini API returned {StatusCode}.", (int)resp.StatusCode);
                throw new AiProviderUnavailableException(ProviderName, $"HTTP {(int)resp.StatusCode} from the provider");
            }

            var body = await resp.Content.ReadAsStringAsync(ct);
            try
            {
                using var doc = JsonDocument.Parse(body);
                var text = doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();

                if (string.IsNullOrWhiteSpace(text))
                {
                    throw new AiProviderUnavailableException(ProviderName, "the response carried no completion text");
                }

                return text;
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException)
            {
                _logger.LogError(ex, "Gemini API response was not in the expected shape.");
                throw new AiProviderUnavailableException(ProviderName, "the response was not in the expected shape", ex);
            }
        }
    }

    private static T DeserializeCompletion<T>(string completion)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(completion, JsonOptions)
                   ?? throw new AiProviderUnavailableException(ProviderName, $"the completion deserialized to null for {typeof(T).Name}");
        }
        catch (JsonException ex)
        {
            throw new AiProviderUnavailableException(ProviderName, $"the completion was not valid JSON for {typeof(T).Name}", ex);
        }
    }

    private static ExecutiveSummaryDto GetExecutiveSummaryStub(GenerateExecutiveSummaryRequest req)
    {
        return new ExecutiveSummaryDto(
            Headline: "Senior Lead Architect candidate with exceptional full-stack credentials and proven team leadership.",
            ExecutiveSummary: "Candidate demonstrates strong alignment with senior engineering leadership requirements. Possesses deep technical expertise in ASP.NET Core, multi-tenant databases, and modern frontend frameworks, combined with strong communication skills.",
            KeyHighlights: new List<string>
            {
                "7+ years experience designing enterprise SaaS backend architectures",
                "Successfully led cross-functional engineering teams of 10+ developers",
                "Proven expertise in dynamic RBAC, domain-driven design, and high-density UI development"
            },
            RecommendedInterviewQuestions: new List<string>
            {
                "How do you handle zero-downtime database migrations in multi-tenant SaaS environments?",
                "Can you walk us through a trade-off decision you made between rapid feature delivery and architectural refactoring?",
                "How do you mentor mid-level software engineers on Clean Architecture principles?"
            }
        );
    }

    private static DocumentPrepResultDto GetDocumentPrepStub(PrepareDocumentRequest req)
    {
        var title = req.DocumentType switch
        {
            "InterviewKit" => "Candidate Interview Kit & Assessment Guide",
            "ClientDossier" => "Executive Candidate Dossier (Client Presentation)",
            _ => "Job Description & Sourcing Brief"
        };

        var markdown = $@"# {title}

## Executive Summary
This document was generated automatically via Gemini AI for Candidate ID `{req.CandidateId}`.

### Core Qualifications
- **Primary Expertise**: Full Stack Engineering (.NET 10 & React TypeScript)
- **Architectural Strength**: Clean Architecture, Microservices, Dynamic RBAC
- **Domain Experience**: B2B Recruitment Agency SaaS (RAaaS)

### Assessment Criteria
1. Technical Competency & Clean Code Standards
2. System Design & Database Multi-Tenancy Scoping
3. Leadership & Cross-Functional Teamwork";

        var html = $@"<div class=""document-prep"">
<h1>{title}</h1>
<h2>Executive Summary</h2>
<p>This document was generated automatically via Gemini AI for Candidate ID <code>{req.CandidateId}</code>.</p>
<h3>Core Qualifications</h3>
<ul>
  <li><strong>Primary Expertise</strong>: Full Stack Engineering (.NET 10 & React TypeScript)</li>
  <li><strong>Architectural Strength</strong>: Clean Architecture, Microservices, Dynamic RBAC</li>
  <li><strong>Domain Experience</strong>: B2B Recruitment Agency SaaS (RAaaS)</li>
</ul>
</div>";

        return new DocumentPrepResultDto(
            DocumentTitle: title,
            ContentMarkdown: markdown,
            ContentHtml: html
        );
    }

    private static BurmeseLocalizationResultDto GetBurmeseLocalizationStub(BurmeseLocalizationRequest req)
    {
        string translated = req.TargetLanguage.ToLowerInvariant() switch
        {
            "my" => "ကိုယ်ရေးအကျဉ်းနှင့် အရည်အချင်းစစ်ဆေးချက်များကို အဆင့်မြင့် AI နည်းပညာဖြင့် စိစစ်သုံးသပ်ပြီးပါပြီ။",
            _ => "Candidate summary and qualification assessments have been analyzed using advanced AI technology."
        };

        return new BurmeseLocalizationResultDto(
            OriginalText: req.SourceText,
            TranslatedText: translated,
            SourceLanguage: req.TargetLanguage == "my" ? "en" : "my",
            TargetLanguage: req.TargetLanguage
        );
    }
}
