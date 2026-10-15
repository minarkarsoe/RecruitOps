# Flow 1 (CV Upload & Local Text Extraction Flow) Codebase Survey Report

## Executive Summary
This report provides a comprehensive architectural and codebase survey of the RecruitOps backend (.NET 10 Clean Architecture in `backend/`) and key frontend integration points in preparation for **Flow 1: CV Upload & Local Text Extraction Flow**.

### Baseline Health Check
- **Backend Test Suite**: `dotnet test backend/RecruitOps.sln` — **333 tests passing** (51 Domain + 282 Api).
- **Frontend Test Suite**: `npm run test` (in `frontend/internal`) — **233 tests passing**.
- **TypeScript Check**: `npm run typecheck` — **0 errors** across all workspaces.

---

## 1. Existing Infrastructure & Foundational Services

### 1.1 Object Storage Abstraction (`IFileStorage`)
- **Interface Path**: `backend/src/Application/Interfaces/IFileStorage.cs`
- **Implementation Path**: `backend/src/Infrastructure/Services/FileStorage/S3FileStorage.cs`
- **Configuration**: Options in `backend/src/Infrastructure/Options/FileStorageOptions.cs`, bound via `Storage` config section.
- **Capabilities**:
  - `UploadAsync(UploadFileRequest, CancellationToken)`: Uploads file stream to S3/MinIO.
  - `DownloadAsync(fileKey, bucketName, CancellationToken)`: Downloads object stream (`StorageObject`).
  - `DeleteAsync(fileKey, bucketName, CancellationToken)`: Deletes file.
  - `GetPresignedUrlAsync(PresignedUrlRequest, CancellationToken)`: Generates presigned URLs with auto-rewriting from internal Docker host (`http://storage:9000`) to public external URL (`http://localhost:9000`).
  - `ExistsAsync` & `GetMetadataAsync`.

### 1.2 Myanmar Script Normalization (`IMyanmarScriptNormalizer`)
- **Interface Path**: `backend/src/Application/Interfaces/IMyanmarScriptNormalizer.cs`
- **Implementation Path**: `backend/src/Infrastructure/Services/MyanmarScript/MyanmarScriptNormalizer.cs`
- **Namespace**: `RecruitOps.Application.Interfaces` / `RecruitOps.Infrastructure.Services.MyanmarScript`
- **Capabilities**:
  - `Normalize(string? input) -> MyanmarScriptNormalizationResult`: In-process Zawgyi detection & conversion to Unicode FormC with confidence scores.
  - `IsZawgyi(string? input) -> bool`: Fast-path detection.
  - Unit & stress tests present in `backend/tests/RecruitOps.Api.Tests/MyanmarScriptNormalizerTests.cs`.

---

## 2. Domain & Application Entity Analysis

### 2.1 `JobApplication` (`backend/src/Domain/Entities/JobApplication.cs`)
- **Current Properties**: `TenantId`, `JobPostingId`, `CandidateId`, `Status` (`PipelineStatus`), `Source` (`SourceChannel`), `AppliedAt`, `CustomFieldsJson`, `CoverNote`.
- **CV / Document Storage Gap**: Currently `JobApplication` has **no properties** linking to stored CV files (e.g. `ResumeFileKey`, `ResumeFileName`, `ResumeContentType`, `ExtractedText`, `ParsingStatus`).

### 2.2 `Candidate` (`backend/src/Domain/Entities/Candidate.cs`)
- **Current Properties**: `TenantId`, `FullName`, `Email`, `Phone`, `Source`, `MergedIntoCandidateId`.
- **Existing TODO**: Line 33: `// TODO (Module 2.3+): Skills, Experience, CvDocument — arrive with OCR/profiling.`
- **Parsing Gap**: Candidate profile attributes (Skills, Work Experience, Education) are not yet persisted as structured columns or JSON columns.

### 2.3 `JobPosting` (`backend/src/Domain/Entities/JobPosting.cs`)
- **Current Properties**: `TenantId`, `DepartmentId`, `RequisitionId`, `Status`, `Title`, `Description`, `Location`, `EmploymentType`, `Headcount`, `SalaryMin`, `SalaryMax`, `ShowSalary`, `ApplicationFormFieldsJson`, `PostedAt`, `ClosedAt`.
- **Bulk Batch Gap**: Missing batch tracking state for multi-file CV uploads against a job posting.

---

## 3. Document Parsing Dependencies Assessment

Inspection of `backend/src/Infrastructure/RecruitOps.Infrastructure.csproj` shows:
- **Installed Packages**: `Microsoft.EntityFrameworkCore`, `Npgsql.EntityFrameworkCore.PostgreSQL`, `AWSSDK.S3` (3.7.400), `System.IdentityModel.Tokens.Jwt`, etc.
- **Document Extraction Libraries Status**: **NONE currently installed**.
- **Required Packages to Add**:
  1. `UglyToad.PdfPig` (MIT license) for local PDF text stream extraction.
  2. `DocumentFormat.OpenXml` (MIT license) for DOCX OpenXML body text extraction.
  3. Image OCR fallback / lightweight OCR capability for scanned PDFs and image files (PNG/JPG).

---

## 4. API Controller & Endpoint Survey

### 4.1 Current Controllers (`backend/src/Api/Controllers/`)
- `ApplicationsController.cs`:
  - `POST api/applications/{id}/stage`
  - `GET api/applications/{id}/history`
- `JobPostingsController.cs`:
  - `GET api/jobpostings`, `GET api/jobpostings/{id}`, `POST api/jobpostings`, `PUT api/jobpostings/{id}`
  - `POST api/jobpostings/{id}/publish`, `POST api/jobpostings/{id}/close`
  - `GET api/jobpostings/{id}/pipeline`
- `AiController.cs`:
  - `POST api/ai/claude/parse-resume` (Accepts raw text string, uses Claude AI)

### 4.2 Required Endpoints for Flow 1
1. **Single Application CV Upload & Extraction**:
   - `POST /api/applications/{id}/resume` (`multipart/form-data`, file up to 10MB)
   - `GET /api/applications/{id}/resume` (File stream or presigned download URL)
2. **Bulk Job Posting CV Ingest**:
   - `POST /api/jobpostings/{jobPostingId}/resumes/bulk` (Accepts up to 50 files)
   - `GET /api/jobpostings/{jobPostingId}/resumes/bulk/{batchId}` (Tracking batch progress)

---

## 5. Frontend Integration Points

- `CandidateSlideOver.tsx` (`frontend/internal/src/features/pipeline/CandidateSlideOver.tsx`):
  - Currently contains tab placeholders for Overview, CV Viewer, Stage History, Scorecards, Notes.
  - Needs CV upload drag-and-drop zone, embedded CV text/document viewer, and Parsed Profile Human Review panel side-by-side with editable fields.
- `JobPostingDetailPage` (`frontend/internal/src/routes/`):
  - Needs Bulk CV Upload Modal with multi-file upload & live batch status progress indicators.

---

## 6. Architecture & Implementation Recommendations

1. **Document Extraction Infrastructure Service (`DocumentExtractionService`)**:
   - Create `IDocumentExtractor` in `Application/Interfaces`.
   - Implement in `Infrastructure/Services/DocumentExtraction/DocumentExtractionService.cs`.
   - Use `PdfPig` for PDFs, `DocumentFormat.OpenXml` / Zip XML parser for DOCX, and fallback for PNG/JPG.
   - Run `IMyanmarScriptNormalizer.Normalize` on all extracted text streams before returning `DocumentExtractionResult`.

2. **Entity Schema Expansion**:
   - Extend `JobApplication` with CV metadata (`ResumeFileKey`, `ResumeFileName`, `ResumeContentType`, `ExtractedText`, `ParsedContactInfoJson`, `IsZawgyiNormalized`).
   - Add `BulkResumeBatch` and `BulkResumeFileState` domain entities (or thread-safe in-memory channel runner) to support batch processing tracking.

3. **Background Ingestion Job Runner**:
   - Implement `IBulkResumeProcessor` with background queue (`Channel<BulkResumeTask>`) or HostedService to process bulk uploads asynchronously without blocking HTTP requests.

4. **Integration with `IMyanmarScriptNormalizer` & `IFileStorage`**:
   - Save original file stream to S3/MinIO using `IFileStorage.UploadAsync`.
   - Perform in-memory text extraction, normalize text using `IMyanmarScriptNormalizer`.
   - Optionally trigger `IAiIntegrationService.ParseResumeAsync` if structured parsing is requested, pre-populating parsed contact/profile fields.

---
*Report generated by teamwork_preview_explorer on 2026-08-07.*
