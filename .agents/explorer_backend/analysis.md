# RecruitOps AI Integration Flow — Backend Architectural Analysis & Design Report

**Author:** Explorer 1 (Backend Specialist)  
**Target Flow:** Person B - Flow 2: AI Integration Flow  
**Date:** 2026-08-11  
**Working Directory:** `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\explorer_backend`  

---

## 1. Executive Summary

This report establishes the complete backend architecture, service abstractions, DTO contracts, API Key Gating design, human confirmation workflow (per ADR-0008), Myanmar script normalization strategy (per ADR-0009), and test strategy for the **AI Integration Flow (Flow 2)** in RecruitOps.

The backend implementation follows **Clean Architecture** principles, maintaining strict separation between Domain entities, Application service contracts, Infrastructure provider integrations, and Api controllers. The current backend test baseline of **411 passing tests** (51 Domain + 360 Api) has been verified green and must remain 100% green upon implementing these enhancements.

---

## 2. Codebase Structure & Architectural Exploration

### 2.1 Backend Architecture Layers

```
backend/
├── src/
│   ├── Domain/                 # Pure domain entities, enums, interfaces, base entity definitions
│   │   ├── Entities/           # Candidate.cs, JobPosting.cs, JobApplication.cs, Note.cs, User.cs
│   │   └── Enums/              # SourceChannel.cs, ApplicationStage.cs
│   ├── Application/            # Application logic, DTO contracts, Service interfaces
│   │   ├── Common/             # ICurrentUser.cs, ICurrentTenant.cs, IDepartmentAccess.cs
│   │   ├── DTOs/Ai/            # AI Request & Response record DTOs
│   │   └── Interfaces/         # IAiIntegrationService.cs, IClaudeService.cs, IGeminiService.cs
│   ├── Infrastructure/         # EF Core DbContext, External Clients, Options, Services
│   │   ├── Options/            # ClaudeOptions.cs, GeminiOptions.cs, FileStorageOptions.cs
│   │   ├── Persistence/        # AppDbContext.cs, Database startup & migrations
│   │   ├── Services/           # AiIntegrationService.cs, ClaudeApiClient.cs, GeminiApiClient.cs
│   │   └── DependencyInjection.cs # Service collection registrations
│   └── Api/                    # ASP.NET Core Web API Controllers, Auth & RBAC Handlers
│       ├── Authorization/      # HasPermissionAttribute.cs, PermissionAuthorizationHandler.cs
│       ├── Controllers/        # AiController.cs, CandidatesController.cs, JobPostingsController.cs
│       └── Program.cs          # Web host bootstrapping, middleware pipeline
└── tests/
    ├── RecruitOps.Domain.Tests/ # 51 unit tests for Domain logic
    └── RecruitOps.Api.Tests/    # 360 integration/API tests using CustomWebAppFactory
```

### 2.2 Existing Service Contracts & DTO Patterns

- **DTO Design Pattern:** All DTOs are immutably declared as C# `record` types with primary constructors and positional parameters.
- **Controller Pattern:** Controllers inherit from `ControllerBase`, use `[ApiController]`, `[Route("api/[controller]")]`, `[Authorize]`, and method-level `[HasPermission("permission:name")]` attributes.
- **Error Response Standard:** Controllers return `ProblemDetails` objects on validation or authorization failures, preserving HTTP standards (400 Bad Request, 402 Payment Required, 403 Forbidden, 404 Not Found).
- **Configuration & Options Pattern:** Strongly typed options classes (`ClaudeOptions`, `GeminiOptions`) bound via `services.Configure<TOptions>(config.GetSection(...))`. Options are injected into services using `IOptions<TOptions>`.

---

## 3. Provider-Agnostic AI Service Interfaces & Endpoint Design

### 3.1 Interface Abstractions

To satisfy ADR-0008 ("Provider-agnostic behind an interface, so a customer can point at a different vendor without a code change"), the AI subsystem is decoupled into three primary interfaces:

1. **`IAiIntegrationService` (Application Layer Orchestrator / Facade)**
   Provides high-level application endpoints used directly by `AiController.cs`. Acts as an orchestrator, delegating task execution to specific AI client providers (Claude or Gemini) based on capabilities.

   ```csharp
   namespace RecruitOps.Application.Interfaces;

   public interface IAiIntegrationService
   {
       Task<ParsedResumeResultDto> ParseResumeAsync(ParseResumeRequest request, CancellationToken ct = default);
       Task<CandidateMatchAnalysisDto> MatchCandidateAsync(MatchCandidateRequest request, CancellationToken ct = default);
       Task<ExecutiveSummaryDto> GenerateExecutiveSummaryAsync(GenerateExecutiveSummaryRequest request, CancellationToken ct = default);
       Task<DocumentPrepResultDto> PrepareDocumentAsync(PrepareDocumentRequest request, CancellationToken ct = default);
       Task<BurmeseLocalizationResultDto> TranslateBurmeseAsync(BurmeseLocalizationRequest request, CancellationToken ct = default);
   }
   ```

2. **`IClaudeService` (Infrastructure Layer — Data Analysis & Smart Matching)**
   Specialized client for Anthropic Claude API (Sonnet 3.5), optimal for complex structured data extraction and contextual reasoning.

   ```csharp
   namespace RecruitOps.Application.Interfaces;

   public interface IClaudeService
   {
       Task<ParsedResumeResultDto> ParseResumeAsync(ParseResumeRequest request, CancellationToken ct = default);
       Task<CandidateMatchAnalysisDto> MatchCandidateAsync(MatchCandidateRequest request, string? candidateProfileData = null, string? jobPostingData = null, CancellationToken ct = default);
   }
   ```

3. **`IGeminiService` (Infrastructure Layer — Document Generation & Localization)**
   Specialized client for Google Gemini API (1.5 Pro / Flash), optimal for long-context document generation, executive summaries, and Burmese ↔ English translations.

   ```csharp
   namespace RecruitOps.Application.Interfaces;

   public interface IGeminiService
   {
       Task<ExecutiveSummaryDto> GenerateExecutiveSummaryAsync(GenerateExecutiveSummaryRequest request, string? candidateProfileData = null, string? jobPostingData = null, CancellationToken ct = default);
       Task<DocumentPrepResultDto> PrepareDocumentAsync(PrepareDocumentRequest request, string? candidateProfileData = null, string? jobPostingData = null, CancellationToken ct = default);
       Task<BurmeseLocalizationResultDto> TranslateBurmeseAsync(BurmeseLocalizationRequest request, CancellationToken ct = default);
   }
   ```

---

### 3.2 The 5 AI Endpoints & Dual Route Mapping

To ensure 100% compliance with `ORIGINAL_REQUEST.md` while remaining 100% backward compatible with existing tests (`EmpiricalAiControllerChallengeTests.cs`), `AiController.cs` exposes primary endpoints with route aliases:

| Endpoint # | Primary Route | Legacy / Alias Route | Provider | Permission Required | Purpose |
|---|---|---|---|---|---|
| **1** | `POST /api/ai/parse-resume` | `POST /api/ai/claude/parse-resume` | Claude | `permission:ai:resume:parse` | CV text → structured candidate JSON |
| **2** | `POST /api/ai/match-candidate` | `POST /api/ai/claude/match-candidate` | Claude | `permission:ai:matching:analyze` | Candidate vs. Job fit score (0-100) & breakdown |
| **3** | `POST /api/ai/executive-summary` | `POST /api/ai/gemini/executive-summary` | Gemini | `permission:ai:summary:generate` | Executive candidate summary & questions |
| **4** | `POST /api/ai/document-prep` | `POST /api/ai/gemini/document-prep` | Gemini | `permission:ai:document:prepare` | Interview kit & dossier generation |
| **5** | `POST /api/ai/translate` | `POST /api/ai/gemini/burmese-localization` | Gemini | `permission:ai:localization:translate` | Burmese ↔ English text localization |

#### Proposed Controller Method Annotations in `AiController.cs`:

```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AiController : ControllerBase
{
    private readonly IAiIntegrationService _aiService;

    public AiController(IAiIntegrationService aiService) => _aiService = aiService;

    [HttpPost("parse-resume")]
    [HttpPost("claude/parse-resume")]
    [HasPermission("permission:ai:resume:parse")]
    public async Task<ActionResult<ParsedResumeResultDto>> ParseResume([FromBody] ParseResumeRequest request, CancellationToken ct);

    [HttpPost("match-candidate")]
    [HttpPost("claude/match-candidate")]
    [HasPermission("permission:ai:matching:analyze")]
    public async Task<ActionResult<CandidateMatchAnalysisDto>> MatchCandidate([FromBody] MatchCandidateRequest request, CancellationToken ct);

    [HttpPost("executive-summary")]
    [HttpPost("gemini/executive-summary")]
    [HasPermission("permission:ai:summary:generate")]
    public async Task<ActionResult<ExecutiveSummaryDto>> GenerateExecutiveSummary([FromBody] GenerateExecutiveSummaryRequest request, CancellationToken ct);

    [HttpPost("document-prep")]
    [HttpPost("gemini/document-prep")]
    [HasPermission("permission:ai:document:prepare")]
    public async Task<ActionResult<DocumentPrepResultDto>> PrepareDocument([FromBody] PrepareDocumentRequest request, CancellationToken ct);

    [HttpPost("translate")]
    [HttpPost("gemini/burmese-localization")]
    [HttpPost("gemini/translate")]
    [HasPermission("permission:ai:localization:translate")]
    public async Task<ActionResult<BurmeseLocalizationResultDto>> BurmeseLocalization([FromBody] BurmeseLocalizationRequest request, CancellationToken ct);
}
```

---

### 3.3 Data Transfer Objects (DTO Definitions)

```csharp
namespace RecruitOps.Application.DTOs.Ai;

// 1. Resume Parsing DTOs
public record ParseResumeRequest(
    string ResumeText,
    string? FileName = null
);

public record ParsedResumeResultDto(
    string FullName,
    string? Email,
    string? Phone,
    string Summary,
    List<WorkExperienceDto> WorkExperiences,
    List<EducationDto> Educations,
    List<string> Skills,
    List<string> Languages,
    int EstimatedYearsOfExperience
);

public record WorkExperienceDto(
    string Company,
    string Position,
    string StartDate,
    string EndDate,
    string Description,
    List<string> Highlights
);

public record EducationDto(
    string Institution,
    string Degree,
    string FieldOfStudy,
    string StartDate,
    string EndDate
);

// 2. Candidate Matching DTOs
public record MatchCandidateRequest(
    Guid CandidateId,
    Guid JobPostingId
);

public record CandidateMatchAnalysisDto(
    int MatchScore, // 0-100 score
    string OverallVerdict, // e.g. "Strong Fit", "Moderate Fit", "Low Alignment"
    List<string> MatchedSkills,
    List<string> MissingSkills,
    List<string> Strengths,
    List<string> Concerns,
    string Recommendation
);

// 3. Executive Summary DTOs
public record GenerateExecutiveSummaryRequest(
    Guid CandidateId,
    Guid? JobPostingId = null,
    string? StylePreference = null
);

public record ExecutiveSummaryDto(
    string Headline,
    string ExecutiveSummary,
    List<string> KeyHighlights,
    List<string> RecommendedInterviewQuestions
);

// 4. Document Prep DTOs
public record PrepareDocumentRequest(
    Guid CandidateId,
    Guid JobPostingId,
    string DocumentType // e.g., "InterviewKit", "ClientDossier", "SourcingBrief"
);

public record DocumentPrepResultDto(
    string DocumentTitle,
    string ContentMarkdown,
    string ContentHtml
);

// 5. Burmese Localization DTOs
public record BurmeseLocalizationRequest(
    string SourceText,
    string TargetLanguage, // "my" for Burmese, "en" for English
    string? ContextType = null
);

public record BurmeseLocalizationResultDto(
    string OriginalText,
    string TranslatedText,
    string SourceLanguage,
    string TargetLanguage
);
```

---

## 4. API Key Gating & Resilience Mechanism Design

Per **ADR-0008** and **R1 Requirements**:
> "If no API key is configured in environment/secrets, endpoints return explicit `402 Payment Required` or feature-disabled response without throwing 500 errors."

### 4.1 Configuration Binding

`ClaudeOptions` and `GeminiOptions` bind API keys from environment variables or `appsettings.json`:

- `AI:Claude:ApiKey` (env override: `AI__Claude__ApiKey`)
- `AI:Gemini:ApiKey` (env override: `AI__Gemini__ApiKey`)

### 4.2 Gating Execution Flow

```
HTTP Request → Controller Action → IAiIntegrationService
                                       │
                    ┌──────────────────┴──────────────────┐
                    ▼                                     ▼
         ClaudeApiClient Check                 GeminiApiClient Check
                    │                                     │
          Is ApiKey configured?                 Is ApiKey configured?
          ├── NO  → Check Env Mode              ├── NO  → Check Env Mode
          │          ├── Production: HTTP 402   │          ├── Production: HTTP 402
          │          └── Development: Fallback  │          └── Development: Fallback
          └── YES → Execute HTTP Call           └── YES → Execute HTTP Call
```

### 4.3 HTTP 402 Payment Required & Feature-Disabled Response Design

When an API Key is missing or invalid in strict/production mode:
1. The service layer returns a key status flag or throws a custom `AiFeatureDisabledException`.
2. The controller catches this and returns `402 Payment Required` formatted as standard `ProblemDetails`:

```json
{
  "type": "https://recruitops.io/errors/ai-feature-disabled",
  "title": "AI Feature Disabled or API Key Unconfigured",
  "status": 402,
  "detail": "The AI feature requires an active API key. Please configure the API key in system settings or environment configuration.",
  "instance": "/api/ai/parse-resume"
}
```

In Development / Sandbox mode (when `ApiKey` is unconfigured), `ClaudeApiClient` and `GeminiApiClient` gracefully provide realistic fallback stubs (e.g. `GetParsedResumeStub`, `GetMatchAnalysisStub`), ensuring developers can work offline without third-party API dependencies.

Crucially, **under no circumstances does an unconfigured API key trigger an unhandled 500 exception or crash the server.**

---

## 5. Human Confirmation Workflow (ADR-0008 Enforcement)

### 5.1 Architectural Principle: Pure Read/Transient Transformation

Per **ADR-0008 Guardrail 1**:
> "Human confirmation is mandatory. AI-extracted PII is never written straight into a candidate profile. Show the parse, let a person accept or correct it. This protects data quality and means an AI error is never silently authoritative."

To enforce this architecturally:
1. **Zero Direct DB Mutations:** None of the 5 AI endpoints (`POST /api/ai/*`) inject `AppDbContext` to execute `DbContext.SaveChanges()` or mutate `Candidate` / `JobPosting` entities.
2. **Transient Payload Return:** Endpoints strictly return in-memory DTOs (`ParsedResumeResultDto`, `CandidateMatchAnalysisDto`, etc.) to the frontend.
3. **Explicit Confirmation Endpoints:** The recruiter inspects the parsed values in the frontend UI (`CandidateSlideOver` / `AiDocumentPrepModal`), makes any necessary adjustments or corrections, and submits a separate explicit mutation request (e.g., `POST /api/candidates` or `PUT /api/candidates/{id}`).

### 5.2 Provenance Persistence

Per **ADR-0008 Guardrail 2**:
> "Persist provenance — raw file, extracted text, parser version, AI output, and a `needs_review` flag."

When a document is uploaded via `IBulkResumeService` or `IResumeService`:
- Raw text and extracted metadata are stored with `IsConfirmed = false` and `NeedsReview = true`.
- Upon recruiter confirmation in UI, the backend candidate record updates `IsConfirmed = true`, recording `ConfirmedByUserId` and `ConfirmedAt` timestamp.

---

## 6. Myanmar Script Handling & Normalization Integration (ADR-0009)

### 6.1 Unicode Normalization at Ingest Boundary

Per **ADR-0009**:
> "Every text entry point detects encoding and converts Zawgyi → Unicode before storage: document extraction, form submissions, and pasted text."

- All incoming text payloads in `ParseResumeRequest` or `BurmeseLocalizationRequest` pass through `IMyanmarScriptNormalizer.Normalize(text)`.
- The canonical normalized Unicode text is passed to Claude / Gemini models and stored in the database.
- Raw text is preserved alongside normalized text for audit and diagnostic purposes.

### 6.2 Burmese ↔ English Localization Endpoint (`/api/ai/translate`)

- Target language `"my"` triggers Burmese translation with proper Unicode output.
- Target language `"en"` translates Burmese CV notes/descriptions back into clear English for hiring managers.
- Supports bilingual generation in `GenerateExecutiveSummary` and `PrepareDocument` when requested.

---

## 7. Backend Test Strategy & Verification Plan

### 7.1 Unit Testing Strategy (`RecruitOps.Api.Tests`)

1. **AI Provider Client Unit Tests (`ClaudeApiClientTests.cs`, `GeminiApiClientTests.cs`):**
   - Mock `HttpMessageHandler` to simulate 200 OK responses with sample JSON payloads.
   - Verify correct request header formatting (`x-api-key`, `anthropic-version`).
   - Mock HTTP 401/429/500 responses to verify graceful error handling.

2. **API Key Gating Fallback Tests (`AiKeyGatingTests.cs`):**
   - Verify unconfigured API key in production mode yields `402 Payment Required`.
   - Verify unconfigured API key in dev mode returns fallback stubs without errors.
   - Confirm 0% 500 error rate across all 5 endpoints when keys are missing.

3. **Match Scoring Calculation Tests (`CandidateMatchTests.cs`):**
   - Test scoring calculations (0–100 scale).
   - Test skill matching, missing skill identification, strengths, and concerns breakdown.

4. **Burmese Script & Translation Tests (`BurmeseLocalizationTests.cs`):**
   - Test Zawgyi-to-Unicode conversion before AI translation.
   - Test translation of mixed English/Burmese text strings.

### 7.2 Regression Baseline Maintenance

- All **411 existing tests** (51 Domain + 360 Api) must pass after any modifications.
- At least **10 new backend unit/integration tests** covering AI endpoints, API key gating, match scoring, and translation will be added.

---

## 8. Conclusion

The proposed backend design provides a robust, scalable, provider-agnostic foundation for Flow 2 (AI Integration Flow). It adheres to Clean Architecture, fully satisfies ADR-0008 (mandatory human review & optional API key gating) and ADR-0009 (Unicode normalization & script handling), and maintains complete test suite integrity.
