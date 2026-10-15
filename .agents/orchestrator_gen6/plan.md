# Project Plan: Person A - Flow 1: CV Upload & Local Text Extraction Flow

## Architecture
- **Backend Architecture**: .NET 10 Clean Architecture.
  - Core domain models & interfaces in `Domain` / `Application` layers.
  - Implementations (`IFileStorage`, document text extractors, OCR fallback, Zawgyi normalizer, background job runners) in `Infrastructure`.
  - HTTP endpoints in `Api` controllers.
- **Frontend Architecture**: React + Vite + TypeScript in `@recruitops/internal` (`frontend/internal`).
  - Feature module: `frontend/internal/src/features/pipeline` and `frontend/internal/src/features/requisitions` / `jobpostings`.
  - Shared UI Primitives in `@recruitops/ui` or `src/components/ui`.
  - Full C# & TypeScript types alignment in `@recruitops/types`.

## Feature Inventory
| # | Feature | Description | Milestone | Source |
|---|---------|-------------|-----------|--------|
| 1 | Single CV Resume Upload API | `POST /api/applications/{id}/resume` accepting PDF/DOCX/PNG/JPG up to 10MB, storing via `IFileStorage` | M1 | ORIGINAL_REQUEST §R1 |
| 2 | CV Resume Download API | `GET /api/applications/{id}/resume` to download/view stored CV file | M1 | ORIGINAL_REQUEST §R1 |
| 3 | Document Extraction Service | PDF text stream extraction, DOCX OpenXML body extraction, image OCR fallback | M1 | ORIGINAL_REQUEST §R1 |
| 4 | Zawgyi Normalization on Extracted Text | Integrated `IMyanmarScriptNormalizer` for automatic Zawgyi->Unicode NFC conversion | M1 | ORIGINAL_REQUEST §R1 |
| 5 | Structured Extraction Response | Returns `extractedText`, `detectedLanguage`, `isZawgyiNormalized`, `parsedContactInfo` | M1 | ORIGINAL_REQUEST §R1 |
| 6 | Bulk CV Upload API | `POST /api/jobpostings/{jobPostingId}/resumes/bulk` accepting up to 50 CV files in a single batch | M2 | ORIGINAL_REQUEST §R2 |
| 7 | Bulk Background Processing Job | Async background worker runner without blocking HTTP requests | M2 | ORIGINAL_REQUEST §R2 |
| 8 | Bulk Status Progress API | `GET /api/jobpostings/{jobPostingId}/resumes/bulk/{batchId}` tracking per-file status (`Queued`, `Processing`, `Success`, `Skipped`, `Failed`) | M2 | ORIGINAL_REQUEST §R2 |
| 9 | Candidate 360 CV & Documents Tab | Drag-and-drop upload zone, progress bar, embedded text viewer in `CandidateSlideOver.tsx` | M3 | ORIGINAL_REQUEST §R3 |
| 10 | Parsed Profile Human Review Panel | Side-by-side extracted text & editable fields (Name, Email, Phone, Experience, Skills) with recruiter confirmation | M3 | ORIGINAL_REQUEST §R3 |
| 11 | JobPostingDetail Bulk CV Upload Modal | Drag-and-drop modal on `JobPostingDetailPage` with per-file live progress bars | M3 | ORIGINAL_REQUEST §R3 |

## Milestones
| # | Name | Scope | Dependencies | Status |
|---|------|-------|-------------|--------|
| M1 | Single CV Storage & Document Text Extraction API | Endpoints `POST /api/applications/{id}/resume`, `GET /api/applications/{id}/resume`, PDF/DOCX/OCR extraction service, `IMyanmarScriptNormalizer` integration, structured result DTOs | none | IN_PROGRESS |
| M2 | Bulk CV Background Processing Job & Status Tracking | Endpoint `POST /api/jobpostings/{jobPostingId}/resumes/bulk`, background job processor, batch tracking state entity/service, `GET /api/jobpostings/{jobPostingId}/resumes/bulk/{batchId}` | M1 | PLANNED |
| M3 | Candidate 360 SlideOver CV Viewer, Parsed Profile Review Panel & Bulk Upload UI | `CandidateSlideOver.tsx` CV & Documents tab, Parsed Profile Confirmation Panel, Bulk Upload Modal on `JobPostingDetailPage`, frontend API services & Vitest tests | M1, M2 | PLANNED |

## Interface Contracts
### Application Layer ↔ Storage & Extraction Infrastructure
- `IFileStorage.UploadAsync(Stream content, string fileName, string contentType, CancellationToken cancellationToken)`
- `IDocumentTextExtractor.ExtractTextAsync(Stream fileStream, string fileName, string contentType, CancellationToken cancellationToken)` -> `DocumentExtractionResult`
- `DocumentExtractionResult`: `{ string ExtractedText, string DetectedLanguage, bool IsZawgyiNormalized, ParsedContactInfo ParsedContactInfo }`
- `IMyanmarScriptNormalizer.NormalizeIfZawgyi(string text)` -> `(string normalizedText, bool wasZawgyi)`

### API Endpoints Spec
- `POST /api/applications/{id}/resume`: `IFormFile file` -> `200 OK` (`ResumeExtractionResponseDto`)
- `GET /api/applications/{id}/resume`: -> `200 OK` (File stream or presigned URL / binary result)
- `POST /api/jobpostings/{jobPostingId}/resumes/bulk`: `List<IFormFile> files` -> `202 Accepted` (`BulkUploadBatchResponseDto { BatchId, TotalFiles, Status }`)
- `GET /api/jobpostings/{jobPostingId}/resumes/bulk/{batchId}`: -> `200 OK` (`BulkBatchStatusDto { BatchId, TotalFiles, ProcessedCount, SuccessCount, FailedCount, Items: List<BulkFileStatusItemDto> }`)
