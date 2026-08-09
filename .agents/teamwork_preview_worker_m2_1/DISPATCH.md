## 2026-08-07T06:32:45Z
You are teamwork_preview_worker for Milestone 2 (Myanmar Script Normalization - Requirement R2).
Your working directory is: c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_worker_m2_1

MANDATORY READS:
- `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\ORIGINAL_REQUEST.md`
- `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\orchestrator\PROJECT.md`
- `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_explorer_survey_2\survey_r2.md`

MANDATORY INTEGRITY WARNING:
DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A teamwork_preview_auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.

Task Scope & Requirements:
1. Create `IMyanmarScriptNormalizer` interface in `backend/src/Application/Interfaces/IMyanmarScriptNormalizer.cs` per specifications in `survey_r2.md` (`Normalize(string? input)` and `IsZawgyi(string? input)`).
2. Implement `MyanmarScriptNormalizer` in `backend/src/Infrastructure/Services/MyanmarScript/MyanmarScriptNormalizer.cs` with in-process Zawgyi detection (codepoint distribution / illegal sequence check), rule-based Zawgyi-to-Unicode conversion, and Unicode NFC normalization (`Normalize(NormalizationForm.FormC)`). Ensure 100% in-process with zero network dependency.
3. Register `IMyanmarScriptNormalizer` in `backend/src/Infrastructure/DependencyInjection.cs` as a Singleton service.
4. Add unit tests in `backend/tests/RecruitOps.Infrastructure.Tests/` (or `RecruitOps.Api.Tests/`) with at least 5 distinct test cases covering:
   - Pure Unicode input (no-op, remains valid Unicode NFC)
   - Zawgyi input (converts correctly to Unicode NFC)
   - Mixed content (preserves non-Myanmar text while normalizing Myanmar script)
   - Empty/null input (returns empty/null gracefully without throwing)
   - Real-world Burmese sentence
5. Execute `dotnet test backend/RecruitOps.sln` to verify all 228 existing tests + new tests pass cleanly.
6. Write progress in `progress.md` and write a detailed `handoff.md` in your working directory.
7. Send a completion message to parent with build/test execution results and list of modified/created files.
