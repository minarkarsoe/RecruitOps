## 2026-08-06T13:20:44Z
Implement Milestone 2 (Hybrid AI API Backend Architecture & Endpoints) following the blueprint in `teamwork_preview_explorer_m2_1_gen5/handoff.md`:
1. Application DTOs: Create DTO files in `backend/src/Application/DTOs/Ai/` (`ParseResumeRequest.cs`, `MatchCandidateRequest.cs`, `GenerateExecutiveSummaryRequest.cs`, `PrepareDocumentRequest.cs`, `BurmeseLocalizationRequest.cs`).
2. Application Interfaces: Create interfaces in `backend/src/Application/Interfaces/` (`IClaudeService.cs`, `IGeminiService.cs`, `IAiIntegrationService.cs`).
3. Infrastructure Options & Clients: Create options in `backend/src/Infrastructure/Options/` (`ClaudeOptions.cs`, `GeminiOptions.cs`), clients & services in `backend/src/Infrastructure/Services/` (`ClaudeApiClient.cs`, `GeminiApiClient.cs`, `AiIntegrationService.cs`), register in `DependencyInjection.cs`, and seed permissions in `RbacSeedData.cs`.
4. Web API Controller: Create `backend/src/Api/Controllers/AiController.cs` with dynamic RBAC attributes:
   - `POST /api/ai/claude/parse-resume` (`permission:ai:resume:parse`)
   - `POST /api/ai/claude/match-candidate` (`permission:ai:matching:analyze`)
   - `POST /api/ai/gemini/executive-summary` (`permission:ai:summary:generate`)
   - `POST /api/ai/gemini/document-prep` (`permission:ai:document:prepare`)
   - `POST /api/ai/gemini/burmese-localization` (`permission:ai:localization:translate`)
5. Integration Tests: Create `backend/tests/RecruitOps.Api.Tests/AiIntegrationTests.cs` testing 401, 403, 400, and 200 OK responses.
6. Verify backend build and tests by running `dotnet build backend/src/Api` and `dotnet test backend/RecruitOps.sln`.
