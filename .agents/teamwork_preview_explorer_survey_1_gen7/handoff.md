# Handoff Report: Milestone 2 Bulk CV Upload Background Job Survey

## 1. Observation
- **Test Suite Status**: Ran `dotnet test backend/RecruitOps.sln` on 2026-08-08T14:58:17Z. Result: **349 tests passed** (51 Domain + 298 Api tests passed in 8s).
- **Existing Endpoints & Controllers**:
  - `ApplicationsController.cs` (`backend/src/Api/Controllers/ApplicationsController.cs:53-95`): Defines `POST /api/applications/{id}/resume` (max 10MB file, `.pdf`, `.docx`, `.png`, `.jpg`, `.jpeg`) and `GET /api/applications/{id}/resume`.
  - `JobPostingsController.cs` (`backend/src/Api/Controllers/JobPostingsController.cs`): Handles postings under route `api/JobPostings`. Decorated with `[Authorize(Policy = Policies.InternalUser)]`.
- **Existing Services**:
  - `ResumeService.cs` (`backend/src/Infrastructure/Services/ResumeService.cs:37-100`): Uses `IFileStorage` to save uploaded files (`applications/{id}/resume/{Guid}_{FileName}`) and `IDocumentTextExtractor` to parse text and contact information.
  - `DocumentTextExtractor.cs` (`backend/src/Infrastructure/Services/DocumentExtraction/DocumentTextExtractor.cs`): Extracts text from PDF, DOCX, PNG, JPG, automatically normalizes Zawgyi to Unicode NFC via `IMyanmarScriptNormalizer`, and parses email, phone, experience, and skills heuristics (`ExtractContactInfo`).
  - `MyanmarScriptNormalizer.cs` (`backend/src/Infrastructure/Services/MyanmarScript/MyanmarScriptNormalizer.cs`): In-process Zawgyi detection and Unicode NFC conversion.
  - `S3FileStorage.cs` (`backend/src/Infrastructure/Services/FileStorage/S3FileStorage.cs`): Implements `IFileStorage` against S3/MinIO.
- **Domain Entities & Scoping**:
  - `JobPosting` (`backend/src/Domain/Entities/JobPosting.cs`): Scoped by `TenantId` and `DepartmentId`.
  - `Candidate` (`backend/src/Domain/Entities/Candidate.cs`): Duplicate detection indexed on `(TenantId, Email)` and `(TenantId, Phone)`.
  - `JobApplication` (`backend/src/Domain/Entities/JobApplication.cs`): Holds `ResumeFileKey`, `ResumeFileName`, `ResumeExtractedText`, `ResumeUploadedAt`, `IsZawgyiNormalized`.
  - `DepartmentAccess.cs` (`backend/src/Infrastructure/Services/DepartmentAccess.cs`): Performs department permission check (`CanAccessAsync(departmentId)`).
- **Existing Bulk/Batch Mechanisms**:
  - Grep search for `bulk` and `batch` across `backend` yielded **0 results**. No background job queue or batch tracking abstractions currently exist.

---

## 2. Logic Chain

1. **Prerequisite Verification**: The current backend code compiles cleanly and passes all 349 unit/integration tests without errors.
2. **Reuse of M1 Foundation**: Single resume upload and text extraction (`ResumeService`, `IDocumentTextExtractor`, `IMyanmarScriptNormalizer`, `IFileStorage`) are fully tested and functional. Milestone 2 can reuse these exact processing components for each file in a batch.
3. **Endpoint Placement & Authorization**: `JobPostingsController` currently manages all job-posting operations. Route `POST /api/jobpostings/{jobPostingId}/resumes/bulk` and status route `GET /api/jobpostings/{jobPostingId}/resumes/bulk/{batchId}` naturally belong under `JobPostingsController.cs` (or a dedicated `BulkResumeController` forwarding to the same route prefix), secured with `IDepartmentAccess` validation on `jobPosting.DepartmentId`.
4. **Asynchronous Execution Model**: Processing up to 50 files per batch synchronously would cause HTTP timeouts. Implementing an asynchronous job manager (`IBulkJobTracker` / `Channel<T>` or `BackgroundService`) allows `POST` requests to return an immediate `202 Accepted` / `200 OK` response with a `batchId`, while progress is tracked in real-time for `GET` progress polling.
5. **Per-File Processing Logic**: Each file in the batch undergoes validation -> text extraction & Zawgyi normalization -> candidate lookup/creation -> application creation -> object storage upload -> stage history logging -> batch status update.

---

## 3. Caveats
- No external message broker (RabbitMQ/Redis) is configured in the environment; all background processing for M2 must use in-process background worker patterns (`BackgroundService` or thread-safe in-memory queue manager).
- Database persistence for batch tracking can be in-memory or EF Core persisted (`BulkResumeBatch` entity). In-memory state tracking works seamlessly for test suites and single-instance backend deployments.

---

## 4. Conclusion
The backend solution is fully prepared for Milestone 2 implementation. The design for `POST /api/jobpostings/{jobPostingId}/resumes/bulk` and `GET /api/jobpostings/{jobPostingId}/resumes/bulk/{batchId}` is documented in `analysis.md` and preserves Clean Architecture, department-level authorization, Zawgyi script normalization, and S3 file storage rules.

---

## 5. Verification Method

To verify the investigation and baseline:

1. **Run Backend Test Suite**:
   ```powershell
   dotnet test backend/RecruitOps.sln
   ```
   *Expected result*: All 349 tests pass (51 Domain + 298 Api).

2. **Inspect Survey Findings File**:
   - View `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_explorer_survey_1_gen7\analysis.md` for complete technical analysis and design specification.
