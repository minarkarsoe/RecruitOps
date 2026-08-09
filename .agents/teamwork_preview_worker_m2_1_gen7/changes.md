# Implementation Report — Milestone 2 (Bulk CV Upload Background Job Backend)

## 1. Overview
This report details the implementation of Milestone 2 (Bulk CV Upload Background Job Backend) for RecruitOps Person A - Flow 1.

The backend implementation enables asynchronous batch CV document ingestion (`POST /api/jobpostings/{jobPostingId}/resumes/bulk`), accepting up to 50 files per batch (.pdf, .docx, .png, .jpg, .jpeg, <=10MB). Each file is processed in a non-blocking background task with real-time progress tracking accessible via `GET /api/jobpostings/{jobPostingId}/resumes/bulk/{batchId}`.

## 2. File Changes & Additions

### Domain & Application Models
- **`backend/src/Domain/Enums/BulkResumeEnums.cs`**:
  - Defined `BulkBatchStatus` (`Queued`, `Processing`, `Completed`, `Failed`).
  - Defined `BulkFileStatus` (`Queued`, `Processing`, `Success`, `Skipped`, `Failed`).
- **`backend/src/Application/DTOs/BulkResumeDtos.cs`**:
  - Created `BulkUploadBatchResponseDto` (`BatchId`, `JobPostingId`, `TotalFiles`, `Status`, `CreatedAt`).
  - Created `BulkBatchStatusDto` (`BatchId`, `JobPostingId`, `Status`, `TotalFiles`, `ProcessedFiles`, `SuccessCount`, `SkippedCount`, `FailedCount`, `CreatedAt`, `CompletedAt`, `Items`).
  - Created `BulkFileItemStatusDto` (`FileName`, `Status`, `ErrorMessage`, `ApplicationId`, `CandidateId`).
  - Created `BulkFileItemInput` (`FileName`, `Content`, `ContentType`).

### Application & Infrastructure Interfaces & Services
- **`backend/src/Application/Common/Interfaces/IBulkResumeService.cs` & `backend/src/Application/Interfaces/IBulkResumeService.cs`**:
  - Defined `IBulkResumeService` interface with `EnqueueBatchAsync` and `GetBatchStatusAsync`.
- **`backend/src/Infrastructure/Services/BulkResumeService.cs`**:
  - Implemented non-blocking background batch processor.
  - Maintains thread-safe batch state in `ConcurrentDictionary<Guid, BatchStateHolder>`.
  - Performs validation for maximum file size (10MB) and allowed extensions (.pdf, .docx, .png, .jpg, .jpeg).
  - Uses `IServiceScopeFactory` to manage DI lifetime during async execution.
  - Calls `IDocumentTextExtractor` (which auto-normalizes Zawgyi script to NFC Unicode via `IMyanmarScriptNormalizer`).
  - Extracts contact info heuristics and deduplicates candidates against `Candidates` table via `ContactNormalizer.Email` and `ContactNormalizer.Phone`.
  - Creates or reuses `Candidate` entity.
  - Creates `JobApplication` with `PipelineStatus.Sourced` and `SourceChannel.Direct`.
  - Uploads CV files to object storage via `IFileStorage`.
  - Logs stage history in `ApplicationStageHistory`.
  - Real-time status updates per file item and batch completion timestamps.
- **`backend/src/Infrastructure/DependencyInjection.cs`**:
  - Registered `IBulkResumeService` with `BulkResumeService` scoped service lifecycle.

### API Controllers
- **`backend/src/Api/Controllers/JobPostingsController.cs`**:
  - Added `POST /api/jobpostings/{jobPostingId}/resumes/bulk`:
    - Validates file count (1..50).
    - Checks job posting existence and department access (`IDepartmentAccess.CanAccessAsync`).
    - Enqueues batch and returns HTTP 200 with `BulkUploadBatchResponseDto`.
  - Added `GET /api/jobpostings/{jobPostingId}/resumes/bulk/{batchId}`:
    - Checks job posting existence and department access.
    - Returns HTTP 200 with `BulkBatchStatusDto`.

### Tests
- **`backend/tests/RecruitOps.Api.Tests/BulkResumeUploadTests.cs`**:
  - `BulkUpload_ValidBatchUpTo50Files_Returns200AndBatchId`
  - `GetBatchStatus_ReturnsPerFileProgressSummary`
  - `BulkUpload_Exceeding50Files_Returns400BadRequest`
  - `BulkUpload_EmptyFileCollection_Returns400BadRequest`
  - `BulkUpload_UnauthorizedDepartmentAccess_Returns404Or403`
  - `BulkUpload_ZawgyiCV_NormalizesExtractedText`
  - `BulkUpload_DuplicateCandidate_ReusesExistingCandidate`
  - `BulkUpload_CorruptOrUnsupportedFile_MarksItemAsFailed`

## 3. Verification & Results
- Command executed: `dotnet test backend/RecruitOps.sln`
- Test Output:
  - `RecruitOps.Domain.Tests.dll`: 51 Passed, 0 Failed, 0 Skipped
  - `RecruitOps.Api.Tests.dll`: 306 Passed, 0 Failed, 0 Skipped
  - Total Passed: **357 tests passed** (exceeding minimum requirement of 357).
