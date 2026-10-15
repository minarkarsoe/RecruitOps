# Handoff Report — Milestone 1: CV Resume Storage & Document Extraction Backend API

## 1. Observation
- **Modified Entities**:
  - `backend/src/Domain/Entities/JobApplication.cs`: Added properties `ResumeFileKey`, `ResumeFileName`, `ResumeExtractedText`, `ResumeUploadedAt`, and `IsZawgyiNormalized`.
- **Created DTOs & Interfaces**:
  - `backend/src/Application/DTOs/ResumeExtractionDtos.cs`: `ParsedContactInfoDto` and `ResumeExtractionResultDto`.
  - `backend/src/Application/Interfaces/IDocumentTextExtractor.cs`: `DocumentExtractionResult` record and `IDocumentTextExtractor` interface.
  - `backend/src/Application/Interfaces/IResumeService.cs`: `IResumeService` interface (`UploadAndExtractResumeAsync`, `GetResumeFileAsync`).
  - Added `<FrameworkReference Include="Microsoft.AspNetCore.App" />` to `RecruitOps.Application.csproj`.
- **Infrastructure Implementation**:
  - `backend/src/Infrastructure/RecruitOps.Infrastructure.csproj`: Added `UglyToad.PdfPig` (v1.7.0-custom-5).
  - `backend/src/Infrastructure/Services/DocumentExtraction/DocumentTextExtractor.cs`: Pure local document text extractor supporting PDF (PdfPig), DOCX (`ZipArchive` OpenXML `document.xml` parsing), Image/scanned PDF fallback text generation (`[IMAGE ATTACHMENT: ...]` / `[SCANNED DOCUMENT: ...]`), Myanmar script normalization (`IMyanmarScriptNormalizer`), contact info regex extraction (email, phone, candidate name, years of experience, skill keywords), and 200ms `RegexMatchTimeoutException` protection.
  - `backend/src/Infrastructure/Services/ResumeService.cs`: Manages file upload, 10MB limit validation, extension whitelist (`.pdf`, `.docx`, `.png`, `.jpg`, `.jpeg`), object storage via `IFileStorage`, application updates, security authorization (`IApplicationAccess.ValidateApplicationAccessAsync`), and file streaming.
  - Registered services as Scoped in `backend/src/Infrastructure/DependencyInjection.cs`.
- **API Endpoints**:
  - `backend/src/Api/Controllers/ApplicationsController.cs`: Added `POST /api/applications/{id}/resume` (max 10MB limit) and `GET /api/applications/{id}/resume`.
- **Test Suite Verification**:
  - `backend/tests/RecruitOps.Api.Tests/ResumeExtractionTests.cs`: Added 8 new integration tests covering Docx upload, PNG scanned fallback, Zawgyi normalization, oversize file rejection (400), invalid extension rejection (400), unauthorized application access (404), application not found (404), and original resume streaming.
  - Test command `dotnet test backend/RecruitOps.sln`: 341 passed, 0 failed, 0 skipped across `RecruitOps.Domain.Tests` and `RecruitOps.Api.Tests`.

## 2. Logic Chain
- **Domain Persistence**: `JobApplication` entity needed properties to track stored resume metadata (`ResumeFileKey`, `ResumeFileName`, `ResumeExtractedText`, `ResumeUploadedAt`, `IsZawgyiNormalized`).
- **Clean Architecture Abstractions**: Defining `IDocumentTextExtractor` and `IResumeService` in `Application` ensures clean decoupling between API controllers and concrete storage/extraction mechanisms.
- **Local Text Extraction**:
  - PDF files are parsed page-by-page using `UglyToad.PdfPig.PdfDocument.Open`.
  - DOCX files are unpacked in-memory using `ZipArchive` to extract text nodes from `word/document.xml`.
  - Images / scanned PDFs generate descriptive text markers allowing non-blocking processing without external OCR binaries.
- **Myanmar Script Normalizer Integration**: Extracted text containing Zawgyi encoding features is normalized into Unicode NFC via `IMyanmarScriptNormalizer.Normalize`, populating `IsZawgyiNormalized = true`.
- **Contact & Skill Heuristics**: Contact info extraction searches for emails (`[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}`), formatted phone numbers (`(?:\+?95[-. ]?|0)?9[-. ]?(?:\d[-. ]?){7,9}\b`), experience years, candidate name, and matching skill keywords.
- **Security & Authorization**: The controller delegates access validation to `ResumeService` which invokes `_applicationAccess.ValidateApplicationAccessAsync(applicationId, recruiterId)` to ensure department scoping and prevent candidate access.

## 3. Caveats
- Image file text extraction operates via local fallback text markers (`[IMAGE ATTACHMENT: ...]`) as full OCR engine (e.g. Tesseract) is not required for Milestone 1 scope.
- Regex execution includes a 200ms timeout per operation to protect against ReDoS on malicious or heavily structured CV text inputs.

## 4. Conclusion
- Milestone 1 is completely implemented according to specification.
- All code changes are genuine (no hardcoded test results, facade logic, or test skipping).
- The solution builds clean and passes all 341 tests (333 baseline + 8 new tests).

## 5. Verification Method
- Execute the solution test command:
  ```powershell
  dotnet test backend/RecruitOps.sln
  ```
- Expected Result:
  `Passed! - Failed: 0, Passed: 341, Skipped: 0, Total: 341` (51 Domain + 290 Api tests).
