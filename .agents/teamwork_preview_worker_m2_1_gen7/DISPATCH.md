## 2026-08-08T08:00:07Z
You are worker_m2_1 (teamwork_preview_worker) for RecruitOps Person A - Flow 1 (Milestone 2).

Working directory: c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_worker_m2_1_gen7

MANDATORY INTEGRITY WARNING:
DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A teamwork_preview_auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.

MANDATORY INPUTS:
1. Original request: c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\ORIGINAL_REQUEST.md
2. Explorer blueprint: c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_explorer_m2_1_gen7\analysis.md
3. Spec miner analysis: c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_spec_miner_survey_3_gen7\analysis.md

YOUR TASK:
Implement Milestone 2 (Bulk CV Upload Background Job Backend):
1. Create DTOs & Enums in `backend/src/Application/DTOs/BulkResumeDtos.cs` and `backend/src/Domain/Enums/BulkResumeEnums.cs`:
   - `BulkBatchStatus` (`Queued`, `Processing`, `Completed`, `Failed`)
   - `BulkFileStatus` (`Queued`, `Processing`, `Success`, `Skipped`, `Failed`)
   - `BulkUploadBatchResponseDto` (`BatchId`, `JobPostingId`, `TotalFiles`, `Status`, `CreatedAt`)
   - `BulkBatchStatusDto` (`BatchId`, `JobPostingId`, `Status`, `TotalFiles`, `ProcessedFiles`, `SuccessCount`, `SkippedCount`, `FailedCount`, `CreatedAt`, `CompletedAt`, `Items`)
   - `BulkFileItemStatusDto` (`FileName`, `Status`, `ErrorMessage`, `ApplicationId`, `CandidateId`)
2. Create `IBulkResumeService` interface in `backend/src/Application/Common/Interfaces/IBulkResumeService.cs`.
3. Create `BulkResumeService` implementation in `backend/src/Infrastructure/Services/BulkResumeService.cs`:
   - Non-blocking asynchronous batch runner (processing up to 50 files per batch).
   - Validates file size (max 10MB) and extensions (.pdf, .docx, .png, .jpg, .jpeg).
   - Uploads files via `IFileStorage`.
   - Extracts text via `IDocumentTextExtractor` (which auto-normalizes Zawgyi via `IMyanmarScriptNormalizer`).
   - Deduplicates candidate via `ContactNormalizer.Email` / `ContactNormalizer.Phone` against `Candidates`. Creates or reuses Candidate.
   - Creates `JobApplication` with `PipelineStatus.Sourced` and `SourceChannel.Direct`.
   - Creates `ApplicationStageHistory`.
   - Updates batch item status in real-time.
   - Register `IBulkResumeService` in `backend/src/Infrastructure/DependencyInjection.cs`.
4. Update `backend/src/Api/Controllers/JobPostingsController.cs`:
   - Add `POST /api/jobpostings/{jobPostingId}/resumes/bulk`: Accept `IFormFileCollection` (up to 50 files), check job posting exists & `IDepartmentAccess`, enqueue batch, return batch tracking ID.
   - Add `GET /api/jobpostings/{jobPostingId}/resumes/bulk/{batchId}`: Check job posting exists & `IDepartmentAccess`, return batch status summary.
5. Create comprehensive tests in `backend/tests/RecruitOps.Api.Tests/BulkResumeUploadTests.cs`:
   - Bulk upload endpoint accepts up to 50 files and returns batch tracking ID.
   - Querying batch status endpoint returns per-file status summary.
   - Rejects batches with > 50 files.
   - Rejects unauthorized department access.
   - Verifies Zawgyi normalization and candidate creation.
6. Verification:
   - Run `dotnet test backend/RecruitOps.sln`. Confirm all 349 existing backend tests + new tests pass cleanly (total >= 357 tests passing).

OUTPUT REQUIREMENTS:
Write your implementation report to `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_worker_m2_1_gen7\changes.md` and handoff report to `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_worker_m2_1_gen7\handoff.md`.
Send message to parent when done.
