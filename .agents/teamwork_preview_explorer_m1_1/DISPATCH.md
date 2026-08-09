## 2026-08-07T14:25:53Z
You are teamwork_preview_explorer working in directory c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_explorer_m1_1.

Objective:
Investigate and design the technical changes for Milestone 1: CV Resume Storage & Document Extraction Backend API.

Inputs to inspect:
- `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\ORIGINAL_REQUEST.md`
- `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_spec_miner_survey_3\spec_analysis.md`
- Codebase in `backend/src/Domain`, `backend/src/Application`, `backend/src/Infrastructure`, `backend/src/Api`.

Tasks:
1. Examine `ApplicationsController.cs` in `backend/src/Api/Controllers/ApplicationsController.cs`.
2. Inspect how `IFileStorage` and `IMyanmarScriptNormalizer` are registered in `backend/src/Infrastructure/DependencyInjection.cs`.
3. Design `IDocumentTextExtractor` interface in `backend/src/Application/Interfaces/IDocumentTextExtractor.cs` and concrete implementation in `backend/src/Infrastructure/Services/DocumentExtraction/DocumentTextExtractor.cs`.
   - Support PDF text stream parsing.
   - Support DOCX OpenXML parsing (`DocumentFormat.OpenXml` or XML body parsing).
   - Support Image/scanned fallback (PNG, JPG, scanned PDF).
   - Pass all extracted text through `IMyanmarScriptNormalizer.NormalizeIfZawgyi()`.
   - Extract basic contact info (Email, Phone, CandidateName, Experience, Skills) via regex / heuristics.
4. Design DTOs in `backend/src/Application/DTOs/ResumeExtractionDtos.cs` (`ResumeExtractionResultDto`, `ParsedContactInfoDto`).
5. Design endpoints on `ApplicationsController`:
   - `POST /api/applications/{id}/resume`
   - `GET /api/applications/{id}/resume`
6. Design unit & integration tests to be added in `backend/tests/RecruitOps.Api.Tests/`.

Output Requirements:
Write complete technical specifications and implementation steps to `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_explorer_m1_1\analysis.md` and `handoff.md`. Send a message to parent when complete.
