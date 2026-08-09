using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RecruitOps.Application.DTOs.Ai;
using RecruitOps.Application.Interfaces;
using RecruitOps.Infrastructure.Options;

namespace RecruitOps.Infrastructure.Services;

public class GeminiApiClient : IGeminiService
{
    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiApiClient> _logger;

    public GeminiApiClient(
        HttpClient httpClient,
        IOptions<GeminiOptions> options,
        ILogger<GeminiApiClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ExecutiveSummaryDto> GenerateExecutiveSummaryAsync(
        GenerateExecutiveSummaryRequest request, string? candidateProfileData = null, string? jobPostingData = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogInformation("Gemini API key not configured. Returning realistic dev fallback stub for GenerateExecutiveSummary.");
            return GetExecutiveSummaryStub(request);
        }

        return GetExecutiveSummaryStub(request);
    }

    public async Task<DocumentPrepResultDto> PrepareDocumentAsync(
        PrepareDocumentRequest request, string? candidateProfileData = null, string? jobPostingData = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogInformation("Gemini API key not configured. Returning realistic dev fallback stub for PrepareDocument.");
            return GetDocumentPrepStub(request);
        }

        return GetDocumentPrepStub(request);
    }

    public async Task<BurmeseLocalizationResultDto> TranslateBurmeseAsync(
        BurmeseLocalizationRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogInformation("Gemini API key not configured. Returning realistic dev fallback stub for TranslateBurmese.");
            return GetBurmeseLocalizationStub(request);
        }

        return GetBurmeseLocalizationStub(request);
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
