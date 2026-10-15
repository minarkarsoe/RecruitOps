# Handoff Report — Milestone 2: Bulk CV Upload Background Job

## 1. Observation

- **Target Module**: RecruitOps Backend (`backend/RecruitOps.sln`).
- **Existing Architecture**:
  - `IResumeService` / `ResumeService` (`backend/src/Infrastructure/Services/ResumeService.cs`): Single resume upload, text extraction, S3/MinIO upload (`IFileStorage`).
  - `IDocumentTextExtractor` / `DocumentTextExtractor` (`backend/src/Infrastructure/Services/DocumentExtraction/DocumentTextExtractor.cs`): PDF stream, OpenXML DOCX, image fallback text extraction, automatically invoking `IMyanmarScriptNormalizer`.
  - `ContactNormalizer` (`backend/src/Domain/ContactNormalizer.cs`): `ContactNormalizer.Email()` (lowercase/trim) and `ContactNormalizer.Phone()` (digit extraction with Myanmar 0095/95 -> 0 prefix replacement).
  - `ApplicationsController.cs` (`backend/src/Api/Controllers/ApplicationsController.cs`): `POST /api/applications/{id}/resume` (max 10MB, PDF/DOCX/PNG/JPG/JPEG).
  - `JobPostingsController.cs` (`backend/src/Api/Controllers/JobPostingsController.cs`): Manages job postings under `/api/jobpostings` with `[Authorize(Policy = Policies.InternalUser)]`.
  - `CustomWebAppFactory.cs` & `Module3Scenario.cs` (`backend/tests/RecruitOps.Api.Tests/`): Standard test harness running with EF Core In-Memory DB and `InMemoryFileStorage`.

---

## 2. Logic Chain

1. **Problem**: Milestone 2 requires bulk CV upload (`POST /api/jobpostings/{jobPostingId}/resumes/bulk`) accepting up to 50 files per batch without blocking HTTP client threads, plus a status summary endpoint (`GET /api/jobpostings/{jobPostingId}/resumes/bulk/{batchId}`).
2. **Data Contracts & Enums**: Defined `BulkUploadBatchResponseDto`, `BulkBatchStatusDto`, `BulkFileItemStatusDto`, `BulkBatchStatus`, and `BulkFileStatus` in `RecruitOps.Application.DTOs` and `RecruitOps.Domain.Enums` matching exact property signatures required by specs.
3. **Background Service & Processing Architecture**:
   - `IBulkResumeService` defines `EnqueueBatchAsync` and `GetBatchStatusAsync`.
   - `BulkResumeService` manages batch state in a thread-safe state store (`ConcurrentDictionary<Guid, BatchStateHolder>`).
   - `EnqueueBatchAsync` validates job posting existance and department access via `IDepartmentAccess`, generates `BatchId`, initializes batch status to `Queued`, launches background worker loop (`Task.Run`), and returns `BulkUploadBatchResponseDto` immediately.
4. **Per-file Background Processing**:
   - Validates file size (max 10MB) and allowed extensions (.pdf, .docx, .png, .jpg, .jpeg).
   - Invokes `IDocumentTextExtractor.ExtractTextAsync` which parses text and auto-converts Zawgyi Myanmar script to Unicode NFC.
   - Extracts contact info heuristics via `DocumentTextExtractor.ExtractContactInfo`.
   - Performs candidate matching via `ContactNormalizer.Email` and `ContactNormalizer.Phone` against `db.Candidates`. Reuses existing candidate if found, or creates a new `Candidate`.
   - Creates `JobApplication` with `PipelineStatus.Sourced` and `SourceChannel.Direct`.
   - Uploads file stream to object storage via `IFileStorage.UploadAsync`.
   - Logs initial stage history entry in `ApplicationStageHistory`.
   - Saves DB changes and updates batch item status to `Success` (or `Failed` with error message if an exception occurs).
5. **Controller Endpoints**:
   - Added `POST /api/jobpostings/{jobPostingId}/resumes/bulk` and `GET /api/jobpostings/{jobPostingId}/resumes/bulk/{batchId}` in `JobPostingsController.cs`.
   - Enforces batch size <= 50 and department access verification.
6. **Testing**:
   - Created test suite specification `BulkResumeUploadTests.cs` covering valid batches, status progress, >50 files rejection, empty batch rejection, unauthorized department access, Zawgyi normalization, duplicate candidate deduplication, and unsupported file handling.

---

## 3. Caveats

- **Read-Only Scope**: This report and accompanying `analysis.md` contain code design blueprints only. No source files were created or modified in `backend/src` or `backend/tests`.
- **In-Memory Job Tracking vs Permanent Database Storage**: The design uses an in-memory thread-safe state manager for high performance and clean test execution. If persistent job state across server restarts is desired in future releases, a database table (`BulkResumeBatches` and `BulkResumeItems`) can be added via EF Core migrations without changing the `IBulkResumeService` contract or controller endpoints.

---

## 4. Conclusion

The specification and code blueprint for Milestone 2 (Bulk CV Upload Background Job) is complete, fully integrated with existing Clean Architecture patterns, object storage abstractions (`IFileStorage`), script normalizers (`IMyanmarScriptNormalizer`), and contact deduplication helpers (`ContactNormalizer`).

---

## 5. Verification Method

To verify the design after implementation:

1. **Build & Test Suite Execution**:
   ```powershell
   dotnet test backend/RecruitOps.sln
   ```
   Confirm all 349 existing backend tests plus the 8 new `BulkResumeUploadTests` pass (total 357+ tests passing).

2. **Endpoints Inspection**:
   - Post 3 sample CV files to `POST /api/jobpostings/{id}/resumes/bulk` -> Expect `200 OK` / `202 Accepted` returning `batchId`.
   - Poll `GET /api/jobpostings/{id}/resumes/bulk/{batchId}` -> Expect status to transition from `Processing` to `Completed` with `items` listing `Success`.
