## 2026-08-07T14:26:54Z
You are teamwork_preview_worker working in directory c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_worker_m1_2.

Objective:
Implement Milestone 1: CV Resume Storage & Document Extraction Backend API.

Context & Reference Materials:
- `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\ORIGINAL_REQUEST.md`
- Explorer handoff: `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_explorer_m1_1\handoff.md`
- Explorer analysis: `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_explorer_m1_1\analysis.md`

Tasks to Execute:
1. Update `JobApplication.cs` (`backend/src/Domain/Entities/JobApplication.cs`) to add properties for resume tracking (`ResumeFileKey`, `ResumeFileName`, `ResumeExtractedText`, `ResumeUploadedAt`, `IsZawgyiNormalized`).
2. Create DTOs `ResumeExtractionDtos.cs` in `backend/src/Application/DTOs/` (`ResumeExtractionResultDto`, `ParsedContactInfoDto`).
3. Create interface `IDocumentTextExtractor.cs` in `backend/src/Application/Interfaces/` and `IResumeService.cs` in `backend/src/Application/Interfaces/`.
4. Create `DocumentTextExtractor.cs` in `backend/src/Infrastructure/Services/DocumentExtraction/DocumentTextExtractor.cs`:
   - Implement PDF text stream extraction (using permissively licensed library such as `UglyToad.PdfPig` if added to csproj or in-memory stream parser).
   - Implement DOCX text extraction using `ZipArchive` body XML parsing (`word/document.xml`).
   - Implement Image / scanned PDF fallback.
   - Run all extracted text through `IMyanmarScriptNormalizer.NormalizeIfZawgyi()`.
   - Implement contact info parsing (Email, Phone, CandidateName, YearsOfExperience, Skills) using regular expressions and keywords.
5. Create `ResumeService.cs` (or implement resume logic) to manage uploading via `IFileStorage`, document text extraction, entity updates, and download stream retrieval.
6. Register `IDocumentTextExtractor` and `IResumeService` in `backend/src/Infrastructure/DependencyInjection.cs`.
7. Add HTTP endpoints in `ApplicationsController.cs` (`backend/src/Api/Controllers/ApplicationsController.cs`):
   - `POST /api/applications/{id}/resume` accepting `IFormFile file` (max 10MB, PDF/DOCX/PNG/JPG). Returns 400 for file >10MB or invalid format, 404 for invalid application. Returns `ResumeExtractionResultDto`.
   - `GET /api/applications/{id}/resume` returning file stream or 404.
8. Create new backend test file `backend/tests/RecruitOps.Api.Tests/ResumeExtractionTests.cs` with at least 8 new tests covering:
   - Successful PDF upload & extraction
   - Successful DOCX upload & extraction
   - Zawgyi normalization on extracted document text
   - File size >10MB rejection (400 Bad Request)
   - Invalid file format rejection (400 Bad Request)
   - Application not found handling (404 Not Found)
   - Resume download endpoint retrieval
   - Contact info heuristic parsing (email/phone/skills)
9. Verify build and tests by running `dotnet test backend/RecruitOps.sln`. All 333 baseline + 8 new tests MUST pass cleanly.

MANDATORY INTEGRITY WARNING:
DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A teamwork_preview_auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.

Output Requirements:
Write your implementation report to `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_worker_m1_2\handoff.md`. Include test execution commands and results. Send a message to parent when done.
