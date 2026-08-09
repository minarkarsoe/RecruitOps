## 2026-08-08T07:59:16Z
<USER_REQUEST>
You are explorer_m2_1 (teamwork_preview_explorer) for RecruitOps Person A - Flow 1 (Milestone 2).

Working directory: c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_explorer_m2_1_gen7

Your task is to produce the concrete code blueprint and step-by-step implementation specification for Milestone 2: Bulk CV Upload Background Job.

MANDATORY INPUTS:
1. Original request: c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\ORIGINAL_REQUEST.md
2. Backend survey analysis: c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_explorer_survey_1_gen7\analysis.md
3. Spec miner analysis: c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_spec_miner_survey_3_gen7\analysis.md

INVESTIGATE & DESIGN:
1. Exact DTO definitions to add to `backend/src/Application/DTOs/` or `backend/src/Application/Common/`:
   - `BulkUploadBatchResponseDto` (`BatchId`, `JobPostingId`, `TotalFiles`, `Status`, `CreatedAt`)
   - `BulkBatchStatusDto` (`BatchId`, `JobPostingId`, `Status`, `TotalFiles`, `ProcessedFiles`, `SuccessCount`, `SkippedCount`, `FailedCount`, `CreatedAt`, `CompletedAt`, `Items`)
   - `BulkFileItemStatusDto` (`FileName`, `Status`, `ErrorMessage`, `ApplicationId`, `CandidateId`)
2. Enums in Domain/Application:
   - `BulkBatchStatus` (`Queued`, `Processing`, `Completed`, `Failed`)
   - `BulkFileStatus` (`Queued`, `Processing`, `Success`, `Skipped`, `Failed`)
3. Interfaces and Services:
   - `IBulkResumeService` (or `IBulkResumeJobManager`): method to enqueue batch `EnqueueBatchAsync(Guid jobPostingId, List<(string FileName, Stream Content, string ContentType)> files, string uploadedByUserId)`, method to get batch status `GetBatchStatusAsync(Guid jobPostingId, Guid batchId)`.
   - Implementation in `Infrastructure/Services/BulkResumeService.cs` (or background queue manager using thread-safe state store/ConcurrentDictionary + Task background worker, ensuring unit tests can await or poll execution cleanly).
   - Per-file background processing steps: validation (max 10MB, allowed extension) -> store via `IFileStorage` -> extract text via `IDocumentTextExtractor` (which auto-normalizes Zawgyi) -> extract contact info -> find or create candidate by email/phone -> create `JobApplication` -> attach resume -> log stage history.
4. Controller Endpoints in `JobPostingsController.cs` (or `BulkResumesController.cs` under `api/jobpostings/{jobPostingId}/resumes/bulk`):
   - `POST /api/jobpostings/{jobPostingId}/resumes/bulk` (accepts `IFormFileCollection` up to 50 files, checks `jobPosting` exists and department access via `IDepartmentAccess`, returns batch response).
   - `GET /api/jobpostings/{jobPostingId}/resumes/bulk/{batchId}` (checks department access, returns `BulkBatchStatusDto`).
5. Unit/Integration Tests in `backend/tests/RecruitOps.Api.Tests/BulkResumeUploadTests.cs`:
   - Test accepting up to 50 files and returning tracking batch ID.
   - Test querying batch status endpoint returning per-file status summary.
   - Test reject batches > 50 files.
   - Test unauthorized department access rejection (403/404).

OUTPUT REQUIREMENTS:
Write detailed implementation blueprint to `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_explorer_m2_1_gen7\analysis.md` and handoff report to `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_explorer_m2_1_gen7\handoff.md`.
Send message to parent when done. Do NOT write or edit source code.
</USER_REQUEST>
