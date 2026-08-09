# Handoff Report: Requirement 3 — Hybrid AI API Integration Survey & Blueprint

## Executive Summary
This report presents a comprehensive investigation and architectural blueprint for **Requirement 3 (Hybrid AI API Integration)** in RecruitOps. Based on an analysis of the existing codebase (`backend/src/`, `frontend/internal/`, `packages/types/`), no AI endpoints or client services currently exist. This document defines the exact architecture, backend endpoints, client service layer, DTO contracts, environment configurations, and testing strategies for integrating **Anthropic Claude API** and **Google Gemini API**.

---

## 1. Observation

### Existing Codebase Analysis
- **Backend Architecture**: ASP.NET Core .NET 10 Clean Architecture monolithic web API (`backend/src/Api/`, `Application/`, `Infrastructure/`, `Domain/`).
  - Controllers sit in `backend/src/Api/Controllers/` and utilize fine-grained authorization via `[HasPermission("permission:module:feature:action")]` policy attributes (e.g., `ApplicationsController.cs`, `CandidatesController.cs`, `InterviewsController.cs`).
  - Infrastructure DI registrations sit in `backend/src/Infrastructure/DependencyInjection.cs`.
  - Application interfaces live in `backend/src/Application/Interfaces/` (e.g., `IRequisitionService.cs`, `IPipelineService.cs`).
  - Auth parameters and system configuration are defined in `backend/src/Api/appsettings.json` and `.env.example`.
  - Integration tests use `WebApplicationFactory<Program>` in `backend/tests/RecruitOps.Api.Tests/CustomWebAppFactory.cs`.
- **Frontend Architecture**:
  - Internal dashboard React SPA in `frontend/internal/src/` using Vite, Vitest (`npm run test`), and `lib/api.ts` fetch wrapper.
  - Shared TypeScript contracts live in `packages/types/src/index.ts`.
- **Current State of AI Code**:
  - Grep searches for `Claude`, `Gemini`, `Anthropic`, `Google`, `Resume`, `Parse` across `backend/src/` returned **0 matching source files**.
  - Requirement 3 is a brand-new feature area requiring complete definition and contract setup.

---

## 2. Logic Chain

### 2.1 Model Selection & Responsibilities Matrix

| AI Model | Primary Responsibilities | Technical Rationale & Architectural Alignment |
|---|---|---|
| **Anthropic Claude API** (`claude-3-5-sonnet`) | 1. **Resume Parsing & Structuring**<br>2. **Candidate Matching Analysis** | - Superior adherence to strict, complex JSON schemas for CV extraction.<br>- Deep reasoning capabilities for candidate-job requirement comparison and skill gap analysis. |
| **Google Gemini API** (`gemini-1.5-pro` / `flash`) | 1. **Executive Summaries**<br>2. **Document Preparation**<br>3. **Burmese Localization** | - High throughput and cost efficiency for generating candidate bios and interview kits.<br>- Exceptional multilingual capabilities, specifically high-fidelity English-to-Burmese / Burmese-to-English translation.<br>- Native Burmese font rendering compliance (line-height ≥ 1.7 for Noto Sans Myanmar per `RecruitOps_Design_System.md`). |

### 2.2 Endpoint Definition & Security Architecture

All AI API endpoints **MUST** be defined in the ASP.NET Core Web API backend (`backend/src/Api/Controllers/AiController.cs`).

**Why Backend Definition?**
1. **API Key Security**: Secret API keys (`ANTHROPIC_API_KEY`, `GEMINI_API_KEY`) stay strictly server-side and are never exposed to browser clients.
2. **Fine-Grained Authorization**: Protect endpoints with `[HasPermission(...)]` policy attributes matching RecruitOps dynamic RBAC.
3. **Data Scoping & ADR-0003**: Enforce department scoping (`IDepartmentAccess`) and tenant isolation (`ICurrentTenant`) on candidates and requisitions passed into AI prompts.
4. **Rate Limiting & Cost Management**: Prevent quota exhaustion by enforcing backend rate limits on expensive AI invocations.

### 2.3 Proposed API Endpoints & Request/Response Contracts

#### 1. Claude: Resume Parsing (`POST /api/ai/claude/parse-resume`)
- **Permission**: `[HasPermission("permission:ai:resume:parse")]` (`Policies.RecruitmentStaff`)
- **Request DTO** (`ParseResumeRequest`):
  ```csharp
  public record ParseResumeRequest(
      string ResumeText,
      string? FileName
  );
  ```
- **Response DTO** (`ParsedResumeResultDto`):
  ```csharp
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
  ```

#### 2. Claude: Candidate Matching (`POST /api/ai/claude/match-candidate`)
- **Permission**: `[HasPermission("permission:ai:matching:analyze")]` (`Policies.RecruitmentStaff`)
- **Request DTO** (`MatchCandidateRequest`):
  ```csharp
  public record MatchCandidateRequest(
      Guid CandidateId,
      Guid JobPostingId
  );
  ```
- **Response DTO** (`CandidateMatchAnalysisDto`):
  ```csharp
  public record CandidateMatchAnalysisDto(
      int MatchScore, // 0 - 100
      string OverallVerdict, // "Strong Fit" | "Moderate Fit" | "Gap Identified"
      List<string> MatchedSkills,
      List<string> MissingSkills,
      List<string> Strengths,
      List<string> Concerns,
      string Recommendation
  );
  ```

#### 3. Gemini: Executive Summaries (`POST /api/ai/gemini/executive-summary`)
- **Permission**: `[HasPermission("permission:ai:summary:generate")]` (`Policies.InternalUser`)
- **Request DTO** (`GenerateExecutiveSummaryRequest`):
  ```csharp
  public record GenerateExecutiveSummaryRequest(
      Guid CandidateId,
      Guid? JobPostingId,
      string? Tone // "Brief" | "Detailed" | "Executive"
  );
  ```
- **Response DTO** (`ExecutiveSummaryDto`):
  ```csharp
  public record ExecutiveSummaryDto(
      string Headline,
      string ExecutiveSummary,
      List<string> KeyHighlights,
      List<string> RecommendedInterviewQuestions
  );
  ```

#### 4. Gemini: Document Preparation (`POST /api/ai/gemini/document-prep`)
- **Permission**: `[HasPermission("permission:ai:document:prepare")]` (`Policies.RecruitmentStaff`)
- **Request DTO** (`PrepareDocumentRequest`):
  ```csharp
  public record PrepareDocumentRequest(
      Guid CandidateId,
      Guid JobPostingId,
      string DocumentType // "InterviewKit" | "ClientDossier" | "JdDraft"
  );
  ```
- **Response DTO** (`DocumentPrepResultDto`):
  ```csharp
  public record DocumentPrepResultDto(
      string DocumentTitle,
      string ContentMarkdown,
      string ContentHtml
  );
  ```

#### 5. Gemini: Burmese Localization (`POST /api/ai/gemini/burmese-localization`)
- **Permission**: `[HasPermission("permission:ai:localization:translate")]` (`Policies.InternalUser`)
- **Request DTO** (`BurmeseLocalizationRequest`):
  ```csharp
  public record BurmeseLocalizationRequest(
      string SourceText,
      string TargetLanguage, // "my" (Burmese) | "en" (English)
      string? Context // "ResumeNote" | "ScorecardComment" | "JobDescription"
  );
  ```
- **Response DTO** (`BurmeseLocalizationResultDto`):
  ```csharp
  public record BurmeseLocalizationResultDto(
      string OriginalText,
      string TranslatedText,
      string SourceLanguage,
      string TargetLanguage
  );
  ```

### 2.4 Infrastructure & Service Layer Design

1. **Interfaces** (`backend/src/Application/Interfaces/`):
   - `IClaudeService.cs`: `ParseResumeAsync(...)`, `MatchCandidateAsync(...)`
   - `IGeminiService.cs`: `GenerateExecutiveSummaryAsync(...)`, `PrepareDocumentAsync(...)`, `TranslateBurmeseAsync(...)`
   - `IAiIntegrationService.cs`: Facade combining business logic, department scoping checks, and AI client calls.

2. **Options Classes** (`backend/src/Infrastructure/Options/`):
   - `ClaudeOptions`: `ApiKey`, `Model` (default: `claude-3-5-sonnet-20241022`), `MaxTokens` (default: 4096), `TimeoutSeconds` (default: 30)
   - `GeminiOptions`: `ApiKey`, `Model` (default: `gemini-1.5-pro`), `TimeoutSeconds` (default: 30)

3. **HTTP Client Implementations** (`backend/src/Infrastructure/Services/`):
   - `ClaudeApiClient.cs`: Uses `HttpClient` registered via `AddHttpClient<IClaudeService, ClaudeApiClient>()`. Includes fallback stub mode when `ApiKey` is blank during dev/testing.
   - `GeminiApiClient.cs`: Uses `HttpClient` registered via `AddHttpClient<IGeminiService, GeminiApiClient>()`. Includes fallback stub mode when `ApiKey` is blank during dev/testing.

4. **Dependency Injection Setup** (`backend/src/Infrastructure/DependencyInjection.cs`):
   ```csharp
   services.Configure<ClaudeOptions>(config.GetSection("AI:Claude"));
   services.Configure<GeminiOptions>(config.GetSection("AI:Gemini"));
   services.AddHttpClient<IClaudeService, ClaudeApiClient>();
   services.AddHttpClient<IGeminiService, GeminiApiClient>();
   services.AddScoped<IAiIntegrationService, AiIntegrationService>();
   ```

### 2.5 Shared Types & Frontend Client Service

1. **`packages/types/src/index.ts`**: Expose mirror interfaces:
   - `ParseResumeRequest`, `ParsedResumeResult`
   - `MatchCandidateRequest`, `CandidateMatchAnalysis`
   - `GenerateExecutiveSummaryRequest`, `ExecutiveSummaryResult`
   - `PrepareDocumentRequest`, `DocumentPrepResult`
   - `BurmeseLocalizationRequest`, `BurmeseLocalizationResult`
2. **`frontend/internal/src/lib/api.ts`**: Add AI helper namespace:
   - `api.ai.parseResume(req)`
   - `api.ai.matchCandidate(req)`
   - `api.ai.generateExecutiveSummary(req)`
   - `api.ai.prepareDocument(req)`
   - `api.ai.translateBurmese(req)`

---

## 3. Caveats

1. **External Network & Key Dependency**: Live calls to Anthropic (`api.anthropic.com`) and Google Gemini (`generativelanguage.googleapis.com`) require valid API keys and outgoing Internet connectivity.
2. **Mocking Strategy**: In dev/test mode without API keys, `ClaudeApiClient` and `GeminiApiClient` MUST provide structured stub responses so frontend developers and test suites can operate seamlessly without external dependencies.
3. **File Extraction**: Resume parsing in initial phase assumes plain text / pre-extracted document text in `ParseResumeRequest.ResumeText`. Binary PDF/DOCX text extraction can be passed through existing object storage services or pre-processed.

---

## 4. Conclusion

The survey and blueprint for Requirement 3 (Hybrid AI API Integration) is complete and fully aligned with RecruitOps architecture and design guidelines:
- **Claude API** handles Resume Parsing & Candidate Matching.
- **Gemini API** handles Executive Summaries, Document Preparation, and Burmese Localization.
- Endpoints are centralized in `backend/src/Api/Controllers/AiController.cs` with dynamic RBAC and department scoping.
- Shared contracts are exposed in `packages/types/src/index.ts`.
- Fallback stubs enable robust offline testing and development.

---

## 5. Verification Method

To verify the AI integration implementation once built:

### Backend Build & Test Commands
1. **Build Backend**:
   ```powershell
   dotnet build backend/src/Api
   ```
2. **Run Backend Integration Tests**:
   ```powershell
   dotnet test backend/RecruitOps.sln --filter "FullyQualifiedName~AiIntegrationTests"
   ```
   Verify that test cases cover:
   - `401 Unauthorized` when calling without Bearer token.
   - `403 Forbidden` when calling without required AI permissions.
   - `400 Bad Request` on invalid inputs (e.g. empty resume text).
   - `200 OK` on successful parsing, matching, summary, document prep, and Burmese translation.

### Frontend Typecheck & Vitest Commands
1. **Typecheck Workspaces**:
   ```powershell
   npm run typecheck
   ```
2. **Run Frontend Tests**:
   ```powershell
   npm run test -- --dir frontend/internal
   ```
   Verify all Vitest tests pass cleanly.
