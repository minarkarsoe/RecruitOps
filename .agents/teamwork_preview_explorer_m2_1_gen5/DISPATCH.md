## 2026-08-06T13:19:11Z
You are an Explorer subagent (teamwork_preview_explorer_m2_1_gen5). Your working directory is `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_explorer_m2_1_gen5`.

Please read:
1. `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\ORIGINAL_REQUEST.md`
2. `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\RecruitOps_Design_System.md`
3. `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\orchestrator_gen5\PROJECT.md`
4. `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_explorer_survey_3_gen5\handoff.md`

Your task:
Analyze Milestone 2 (Hybrid AI API Backend Architecture & Endpoints) and produce a detailed, step-by-step implementation plan for the Worker:
1. Application DTO Records (`backend/src/Application/DTOs/Ai/`):
   - `ParseResumeRequest`, `ParsedResumeResultDto`, `WorkExperienceDto`, `EducationDto`
   - `MatchCandidateRequest`, `CandidateMatchAnalysisDto`
   - `GenerateExecutiveSummaryRequest`, `ExecutiveSummaryDto`
   - `PrepareDocumentRequest`, `DocumentPrepResultDto`
   - `BurmeseLocalizationRequest`, `BurmeseLocalizationResultDto`
2. Application Interfaces (`backend/src/Application/Interfaces/`):
   - `IClaudeService.cs`
   - `IGeminiService.cs`
   - `IAiIntegrationService.cs`
3. Infrastructure Options & Clients (`backend/src/Infrastructure/Options/` & `Services/`):
   - `ClaudeOptions.cs`, `GeminiOptions.cs`
   - `ClaudeApiClient.cs` (with realistic dev fallback stubs when API key is unconfigured)
   - `GeminiApiClient.cs` (with realistic dev fallback stubs when API key is unconfigured)
   - `AiIntegrationService.cs`
   - DI registrations in `backend/src/Infrastructure/DependencyInjection.cs`
4. Web API Controller (`backend/src/Api/Controllers/AiController.cs`):
   - `POST /api/ai/claude/parse-resume` (`permission:ai:resume:parse`)
   - `POST /api/ai/claude/match-candidate` (`permission:ai:matching:analyze`)
   - `POST /api/ai/gemini/executive-summary` (`permission:ai:summary:generate`)
   - `POST /api/ai/gemini/document-prep` (`permission:ai:document:prepare`)
   - `POST /api/ai/gemini/burmese-localization` (`permission:ai:localization:translate`)
5. Integration Tests (`backend/tests/RecruitOps.Api.Tests/AiIntegrationTests.cs`):
   - Test authentication, RBAC authorization (`401`, `403`), input validation (`400`), and successful responses (`200 OK`).

Write your implementation plan and handoff to `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_explorer_m2_1_gen5\handoff.md`.
Send a completion message when done.
