# Handoff Report: Milestone 1 - CV Resume Storage & Document Extraction Backend API

**Agent:** teamwork_preview_explorer  
**Working Directory:** `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_explorer_m1_1`  
**Date:** 2026-08-07  

---

## 1. Observation

- **`ApplicationsController.cs` (`backend/src/Api/Controllers/ApplicationsController.cs`)**:
  - Controller handles application stage changes (`POST /api/applications/{id}/stage`) and stage history (`GET /api/applications/{id}/history`).
  - Lacks resume file upload (`POST /api/applications/{id}/resume`) and resume download (`GET /api/applications/{id}/resume`) endpoints.
- **`DependencyInjection.cs` (`backend/src/Infrastructure/DependencyInjection.cs`)**:
  - `IFileStorage` registered as `Scoped` using `S3FileStorage` (lines 83–95).
  - `IMyanmarScriptNormalizer` registered as `Singleton` using `MyanmarScriptNormalizer` (lines 97–98).
  - Currently missing registration for `IDocumentTextExtractor` and `IResumeService`.
- **Domain & Persistence**:
  - `JobApplication` entity (`backend/src/Domain/Entities/JobApplication.cs`) requires additional fields for tracking stored resume files (`ResumeFileKey`, `ResumeFileName`, `ResumeExtractedText`, `ResumeUploadedAt`).
- **Architectural & Compliance ADRs**:
  - **ADR-0008**: Local document text extraction mandatory (PDF text stream parsing, DOCX OpenXML parsing, image OCR fallback, permissive licensing only).
  - **ADR-0009**: Ingested document text must be normalized via `IMyanmarScriptNormalizer` (Zawgyi to Unicode NFC).
  - **ADR-0013**: Storage operations must route through `IFileStorage` abstraction.
- **Test Baseline**:
  - 333 passing backend tests (`dotnet test backend/RecruitOps.sln`).

---

## 2. Logic Chain

1. **Storage & Ingestion Security**:
   - `POST /api/applications/{id}/resume` receives candidate CV files, enforces file size limit (<= 10MB) and format whitelist (`.pdf`, `.docx`, `.png`, `.jpg`, `.jpeg`).
   - Verifies caller permissions via `IDepartmentAccess` and `ICurrentUser.IsExcludedFromCandidateData` to maintain department isolation and candidate privacy.
2. **Text Extraction & Normalization**:
   - `DocumentTextExtractor` processes PDF text streams using `UglyToad.PdfPig` / `PdfSharpCore`, DOCX body XML using `ZipArchive` / OpenXML, and images using OCR fallback.
   - All extracted raw text passes through `IMyanmarScriptNormalizer.Normalize()` to ensure canonical Unicode (NFC) representation, recording `IsZawgyiNormalized` status.
   - Basic candidate profile fields (Email, Phone, Candidate Name, Experience Years, Skills) are extracted via regex and keyword matching heuristics to pre-fill the frontend human-review gate.
3. **Persistence & Storage**:
   - Uploaded files are stored in object storage (MinIO/R2) under key `applications/{id}/resume/{fileGuid}_{fileName}` via `IFileStorage.UploadAsync()`.
   - Metadata is saved on `JobApplication` entity in the database.
4. **Retrieval**:
   - `GET /api/applications/{id}/resume` fetches stored resume stream via `IFileStorage.DownloadAsync()` for recruiter viewing inside candidate profile drawers.

---

## 3. Caveats

- **Local Image OCR Fallback**: If native Tesseract C libraries are uninstalled in local development environments, image extraction gracefully returns an extraction stub/metadata without throwing unhandled exceptions.
- **Heuristic Parsing Accuracy**: Parsed contact info (Name, Email, Phone, Experience, Skills) is heuristic and serving as an initial pre-fill for the recruiter human-review panel (ADR-0008 Guardrail 1). Recruiter confirmation is required before updating candidate profile records.

---

## 4. Conclusion

The technical design for Milestone 1 (CV Resume Storage & Document Extraction Backend API) is complete, fully specified in `analysis.md`, and ready for implementation. The proposed architecture adheres strictly to Clean Architecture, ADR requirements, permissive licensing constraints, and existing test guardrails.

---

## 5. Verification Method

To independently verify the implementation:
1. **Compilation Check**:
   ```bash
   dotnet build backend/src/Api/RecruitOps.Api.csproj
   ```
2. **Test Suite Verification**:
   ```bash
   dotnet test backend/RecruitOps.sln
   ```
   Confirm all 333 existing backend tests plus new `ResumeExtractionTests` pass cleanly.
3. **Endpoint Spot Check**:
   - Verify `POST /api/applications/{id}/resume` with a sample PDF resume file returns `200 OK` with `ResumeExtractionResultDto`.
   - Verify `GET /api/applications/{id}/resume` returns the file stream.
   - Verify requests exceeding 10MB or containing invalid extensions return `400 Bad Request`.
