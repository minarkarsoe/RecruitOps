# Handoff Report — Flow 1 Codebase Survey

## 1. Observation
1. **Existing Baseline Tests**:
   - Command: `dotnet test backend/RecruitOps.sln`
   - Result: 333 tests passing (51 Domain + 282 Api). 0 failures.
   - Command: `npm run test` (in `frontend/internal`)
   - Result: 233 tests passing. 0 failures.
   - Command: `npm run typecheck`
   - Result: 0 errors.

2. **Existing Storage & Normalization Infrastructures**:
   - `IFileStorage.cs` at `backend/src/Application/Interfaces/IFileStorage.cs` (lines 1–37) defines `UploadAsync`, `DownloadAsync`, `DeleteAsync`, `GetPresignedUrlAsync`, `ExistsAsync`, `GetMetadataAsync`.
   - `S3FileStorage.cs` at `backend/src/Infrastructure/Services/FileStorage/S3FileStorage.cs` (lines 12–250) implements `IFileStorage` using `AWSSDK.S3` v3.7.400.
   - `IMyanmarScriptNormalizer.cs` at `backend/src/Application/Interfaces/IMyanmarScriptNormalizer.cs` (lines 21–35) defines `Normalize(string? input)` and `IsZawgyi(string? input)`.
   - `MyanmarScriptNormalizer.cs` at `backend/src/Infrastructure/Services/MyanmarScript/MyanmarScriptNormalizer.cs` (lines 7–207) implements in-process Zawgyi detection & Unicode FormC normalization.

3. **Data Entities State**:
   - `JobApplication.cs` at `backend/src/Domain/Entities/JobApplication.cs` (lines 10–28) currently tracks `TenantId`, `JobPostingId`, `CandidateId`, `Status`, `Source`, `AppliedAt`, `CustomFieldsJson`, `CoverNote`. It does **not** contain file keys or extracted text attributes for CV resumes.
   - `Candidate.cs` at `backend/src/Domain/Entities/Candidate.cs` line 33 contains: `// TODO (Module 2.3+): Skills, Experience, CvDocument — arrive with OCR/profiling.`

4. **Document Extraction Dependencies**:
   - `RecruitOps.Infrastructure.csproj` at `backend/src/Infrastructure/RecruitOps.Infrastructure.csproj` (lines 1–30) currently contains EF Core, PostgreSQL, S3 AWS SDK, System.IdentityModel.Tokens.Jwt, but **no PDF, DOCX, or OCR parsing packages** installed.

5. **Controllers & Endpoints**:
   - `ApplicationsController.cs` at `backend/src/Api/Controllers/ApplicationsController.cs` contains `/api/applications/{id}/stage` and `/api/applications/{id}/history`. Missing single CV upload (`POST /api/applications/{id}/resume`) and download (`GET /api/applications/{id}/resume`).
   - `JobPostingsController.cs` at `backend/src/Api/Controllers/JobPostingsController.cs` contains CRUD endpoints and `/pipeline`. Missing bulk CV upload (`POST /api/jobpostings/{jobPostingId}/resumes/bulk`) and bulk batch status (`GET /api/jobpostings/{jobPostingId}/resumes/bulk/{batchId}`).

6. **Frontend Component**:
   - `CandidateSlideOver.tsx` at `frontend/internal/src/features/pipeline/CandidateSlideOver.tsx` (lines 173–194) contains a static CV preview placeholder tab.

---

## 2. Logic Chain
1. **Observation 1 & 2** confirm that the foundation built in Sprint 0 (`IFileStorage` with `S3FileStorage` and `IMyanmarScriptNormalizer` with `MyanmarScriptNormalizer`) is fully functional, tested, and ready to be consumed by Flow 1 features.
2. **Observation 3** shows that while `JobApplication` exists in Domain, it requires extension (or related schema mapping) to persist `ResumeFileKey`, `ResumeFileName`, `ExtractedText`, `IsZawgyiNormalized`, and parsed profile metadata.
3. **Observation 4** shows that PDF (`UglyToad.PdfPig`) and DOCX (`DocumentFormat.OpenXml`) parsing packages must be added to `RecruitOps.Infrastructure.csproj` (under permissive licenses like MIT) to extract text locally without relying on external SaaS.
4. **Observation 5** identifies the exact missing API endpoints on `ApplicationsController` and `JobPostingsController` required by the Flow 1 specification.
5. **Observation 6** identifies that `CandidateSlideOver.tsx` needs a functional drag-and-drop upload zone, text extraction preview, and parsed profile human-review confirmation panel.

---

## 3. Caveats
- Image OCR fallback for scanned PDFs or PNG/JPG images depends on lightweight local image processing/OCR or structured fallback; performance and dependency constraints should be verified during implementation.
- Bulk processing of up to 50 CV files requires an in-memory background job queue (`System.Threading.Channels` or `IHostedService`) to prevent HTTP request timeouts.

---

## 4. Conclusion
The codebase is clean, well-tested (333 backend tests + 233 frontend tests passing), and possesses solid foundation interfaces (`IFileStorage`, `IMyanmarScriptNormalizer`). All prerequisites for Flow 1 (CV Upload & Local Text Extraction Flow) are mapped out, and implementation can proceed with high confidence following the architecture outlined in `analysis.md`.

---

## 5. Verification Method
- Baseline verification command: `dotnet test backend/RecruitOps.sln`
- Target file verification:
  - `backend/src/Application/Interfaces/IFileStorage.cs`
  - `backend/src/Infrastructure/Services/FileStorage/S3FileStorage.cs`
  - `backend/src/Application/Interfaces/IMyanmarScriptNormalizer.cs`
  - `backend/src/Infrastructure/Services/MyanmarScript/MyanmarScriptNormalizer.cs`
  - `backend/src/Domain/Entities/JobApplication.cs`
  - `frontend/internal/src/features/pipeline/CandidateSlideOver.tsx`
- Invalidation condition: Any test regression under `dotnet test backend/RecruitOps.sln` or `npm run test` in `frontend/internal`.
