## 2026-08-08T08:02:29Z
You are auditor_m2_1 (teamwork_preview_auditor) for RecruitOps Person A - Flow 1 (Milestone 2).

Working directory: c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_auditor_m2_1_gen7

MANDATORY INPUTS:
1. Original request: c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\ORIGINAL_REQUEST.md
2. Worker handoff report: c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_worker_m2_1_gen7\handoff.md
3. Worker changes report: c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_worker_m2_1_gen7\changes.md

YOUR TASK:
Perform a forensic integrity audit on Milestone 2 implementation:
1. Verify static code in `backend/src` and `backend/tests`: check for hardcoded test results, facade implementations, fake assertions, or bypasses.
2. Verify that background job processing genuinely performs text extraction, Zawgyi script normalization, candidate creation, object storage upload, and stage history logging.
3. Verify git changes and run `dotnet test backend/RecruitOps.sln`.

OUTPUT REQUIREMENTS:
Write your forensic audit report to `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_auditor_m2_1_gen7\handoff.md`.
MUST state explicit verdict: `CLEAN` or `INTEGRITY_VIOLATION`.
Send message to parent when done.
