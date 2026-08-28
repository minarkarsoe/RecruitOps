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
            + $"{LanguageInstruction(request.Language)}"
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

    /// <summary>Turns the requested output language into a prompt instruction (ADR-0009).
    ///
    /// <para><b>Unicode is stated explicitly, and that is the whole point.</b> Burmese has two
    /// incompatible encodings occupying the same code block, and a model asked for "Burmese"
    /// with no further steer can return Zawgyi — which renders as garbage, never matches a
    /// search, and is indistinguishable from Unicode to anything that does not check. ADR-0009
    /// makes Unicode the only representation this system stores.</para>
    ///
    /// <para>An unrecognised value produces no instruction rather than an error: the caller has
    /// asked for something this build does not know, and English is the safe default. The API is
    /// not the place to reject a language code the UI may add next week.</para></summary>
    private static string LanguageInstruction(string? language) => language?.ToLowerInvariant() switch
    {
        "my" => "Write the entire response in Burmese (Myanmar), using Unicode encoding only — never Zawgyi. ",
        "bilingual" => "Write each field in English first, then the same content in Burmese (Myanmar) "
                     + "using Unicode encoding only — never Zawgyi — separated by a blank line. ",
        _ => string.Empty,
    };

    private static ExecutiveSummaryDto GetExecutiveSummaryStub(GenerateExecutiveSummaryRequest req)
    {
        // The stub honours `Language` too. Without it the selector would look broken on every
        // machine without an API key — which is every developer machine — and "the feature does
        // not work locally" is how a working feature gets reported as a bug.
        if (req.Language?.ToLowerInvariant() is "my" or "bilingual")
        {
            return GetBurmeseExecutiveSummaryStub(req.Language.ToLowerInvariant() == "bilingual");
        }

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

    /// <summary>The Burmese sample, in Unicode. Written out rather than machine-translated at
    /// runtime so the stub costs nothing and cannot itself introduce Zawgyi.
    ///
    /// <para>⚠️ Burmese copy pending native review — this is a developer placeholder, the same
    /// caveat the design kit carries on its Burmese strings.</para></summary>
    private static ExecutiveSummaryDto GetBurmeseExecutiveSummaryStub(bool bilingual)
    {
        // Bilingual puts English first, then Burmese after a blank line, matching the prompt
        // instruction sent to the real model so both paths render the same shape.
        string Pair(string en, string my) => bilingual ? $"{en}\n\n{my}" : my;

        return new ExecutiveSummaryDto(
            Headline: Pair(
                "Senior Lead Architect candidate with exceptional full-stack credentials and proven team leadership.",
                "ပြည့်စုံသော full-stack အရည်အချင်းနှင့် အဖွဲ့ဦးဆောင်မှု အတွေ့အကြုံရှိသော အကြီးတန်း ဗိသုကာအင်ဂျင်နီယာ လျှောက်ထားသူ။"),
            ExecutiveSummary: Pair(
                "Candidate demonstrates strong alignment with senior engineering leadership requirements.",
                "လျှောက်ထားသူသည် အကြီးတန်း အင်ဂျင်နီယာ ဦးဆောင်မှုဆိုင်ရာ လိုအပ်ချက်များနှင့် အလွန်ကိုက်ညီပါသည်။ ASP.NET Core၊ multi-tenant ဒေတာဘေ့စ်နှင့် ခေတ်မီ frontend framework များတွင် နက်ရှိုင်းသော ကျွမ်းကျင်မှုရှိပါသည်။"),
            KeyHighlights: new List<string>
            {
                Pair("7+ years designing enterprise SaaS backend architectures",
                     "လုပ်ငန်းသုံး SaaS backend ဗိသုကာ ဒီဇိုင်းရေးဆွဲမှု အတွေ့အကြုံ ၇ နှစ်ကျော်"),
                Pair("Led cross-functional engineering teams of 10+ developers",
                     "ဆော့ဖ်ဝဲရေးသားသူ ၁၀ ဦးကျော်ပါဝင်သော အဖွဲ့များကို ဦးဆောင်ခဲ့သည်"),
                Pair("Expertise in dynamic RBAC and domain-driven design",
                     "dynamic RBAC နှင့် domain-driven design တွင် ကျွမ်းကျင်မှု"),
            },
            RecommendedInterviewQuestions: new List<string>
            {
                Pair("How do you handle zero-downtime database migrations?",
                     "ဝန်ဆောင်မှု မရပ်တန့်ဘဲ ဒေတာဘေ့စ် ပြောင်းလဲမှုများကို မည်သို့ ကိုင်တွယ်ပါသလဲ။"),
                Pair("Describe a trade-off between delivery speed and refactoring.",
                     "လုပ်ငန်းအမြန်ပြီးစီးမှုနှင့် code ပြန်လည်ပြင်ဆင်မှုကြား ရွေးချယ်ခဲ့ရသည့် အခြေအနေတစ်ခုကို ပြောပြပါ။"),
                Pair("How do you mentor mid-level engineers on Clean Architecture?",
                     "အလယ်အလတ်တန်း အင်ဂျင်နီယာများကို Clean Architecture အကြောင်း မည်သို့ လမ်းညွှန်ပါသလဲ။"),
            }
        );
    }

    private static DocumentPrepResultDto GetDocumentPrepStub(PrepareDocumentRequest req)
    {
        // `ClientDossier` was the middle arm until 2026-08-28. It was an agency-era artefact —
        // a candidate packaged for presentation to a client — and ADR-0001 removed clients on
        // 2026-07-27. The controller now rejects anything outside `DocumentTypes.All`, so the
        // default arm is reached only by `JdDraft`.
        var title = req.DocumentType switch
        {
            DocumentTypes.InterviewKit => "Candidate Interview Kit & Assessment Guide",
            _ => "Job Description & Sourcing Brief"
        };

        var markdown = $@"# {title}

## Executive Summary
This document was generated automatically via Gemini AI for Candidate ID `{req.CandidateId}`.

### Core Qualifications
- **Primary Expertise**: Full Stack Engineering (.NET 10 & React TypeScript)
- **Architectural Strength**: Clean Architecture, Microservices, Dynamic RBAC
- **Domain Experience**: In-house talent acquisition platforms

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
  <li><strong>Domain Experience</strong>: In-house talent acquisition platforms</li>
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
