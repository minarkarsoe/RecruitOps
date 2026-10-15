## 2026-08-11T15:06:37Z
You are Worker 1 (Backend Implementation Specialist) for Milestone 1: AI Provider Abstraction & 5 Gated Endpoints.
Your working directory is: c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\worker_m1_backend

MANDATORY INSTRUCTION: You MUST read the original request file at:
c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\ORIGINAL_REQUEST.md
Also read ADR-0008 (`docs/decisions/ADR-0008-document-extraction-and-ai-profiling.md`), ADR-0009 (`docs/decisions/ADR-0009-myanmar-script-handling.md`), and the detailed backend exploration report at:
`c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\explorer_backend\analysis.md`

MANDATORY INTEGRITY WARNING:
DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A teamwork_preview_auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.

Objectives:
1. Implement or verify provider-agnostic interfaces in `backend/src/Application/Interfaces/`:
   - `IAiIntegrationService.cs`
   - `IClaudeService.cs`
   - `IGeminiService.cs`
2. Implement AI DTOs in `backend/src/Application/DTOs/Ai/AiIntegrationDtos.cs`.
3. Implement/refine `ClaudeApiClient.cs`, `GeminiApiClient.cs`, and `AiIntegrationService.cs` in `backend/src/Infrastructure/Services/`.
4. Configure options & API Key Gating in `ClaudeOptions.cs`, `GeminiOptions.cs`, and `AiController.cs`:
   - If API keys are unconfigured, endpoints MUST gracefully return HTTP 402 Payment Required (`ProblemDetails`) without 500 server crashes.
5. Expose 5 dual-routed endpoints in `backend/src/Api/Controllers/AiController.cs`:
   - `POST /api/ai/parse-resume` and `/api/ai/claude/parse-resume`
   - `POST /api/ai/match-candidate` and `/api/ai/claude/match-candidate`
   - `POST /api/ai/executive-summary` and `/api/ai/gemini/executive-summary`
   - `POST /api/ai/document-prep` and `/api/ai/gemini/document-prep`
   - `POST /api/ai/translate` and `/api/ai/gemini/burmese-localization`
6. Enforce ADR-0008 (stateless transformation queries; explicit human confirmation before DB mutation) and ADR-0009 (Zawgyi script normalization via `IMyanmarScriptNormalizer`).
7. Write at least 10 new backend unit/integration tests covering:
   - AI provider client mocking
   - API key gating 402 fallback
   - Candidate match scoring calculation
   - Translation endpoints with Burmese text
8. Run the full backend test suite: `dotnet test backend/RecruitOps.sln`
   - Verify all 411 existing tests pass + at least 10 new tests pass (Total >= 421 tests).

Write your implementation log to:
`c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\worker_m1_backend\changes.md`
and write your handoff report (including build and test execution results) to:
`c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\worker_m1_backend\handoff.md`

Update progress.md in your directory as you work. Send a message to parent when complete with the path to handoff.md.
