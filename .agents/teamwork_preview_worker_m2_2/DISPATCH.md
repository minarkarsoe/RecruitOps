## 2026-08-07T06:40:34Z
<USER_REQUEST>
You are teamwork_preview_worker for Milestone 2 Retry 1 (Myanmar Script Normalization R2 Remediation).
Your working directory is: c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_worker_m2_2

MANDATORY READS:
- `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\ORIGINAL_REQUEST.md`
- `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\orchestrator\PROJECT.md`
- `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_explorer_m2_retry_1\remediation_spec.md`

MANDATORY INTEGRITY WARNING:
DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A teamwork_preview_auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.

Task Scope & Requirements:
1. Update `backend/src/Infrastructure/Services/MyanmarScript/MyanmarScriptNormalizer.cs` per `remediation_spec.md`:
   - Remove `|[\u1000-\u1021]\u103A[\u1000-\u1021]` from `ZawgyiExclusiveRegex` (line 20) to eliminate false-positive Zawgyi detection on standard Unicode Asat sequences.
   - Remove rule 87 (`([\u1000-\u1021])\u103A([\u1000-\u1021])` -> `$1\u1039$2`) from `SubjoinedRules` so Asat (`\u103A`) is not converted to Virama (`\u1039`).
   - Update rule 52 in `SubjoinedRules` for `\u1062` (Kinzi) to map `\u1004\u1062` -> `\u1004\u1039\u1002` and standalone `\u1062` -> `\u1004\u1039\u1002`.
2. Run `dotnet test backend/RecruitOps.sln`.
3. Verify that ALL tests pass cleanly with 0 failures (including `MyanmarScriptNormalizerTests.cs`, `MyanmarScriptNormalizerChallengerTests.cs`, and `MyanmarScriptNormalizerStressTests.cs`).
4. Record progress in `progress.md` and write a detailed `handoff.md` in your working directory.
5. Send a completion message to parent with build/test execution results and list of modified files.
</USER_REQUEST>
