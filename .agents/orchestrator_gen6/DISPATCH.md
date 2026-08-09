## 2026-08-07T14:24:22Z

You are the Project Orchestrator (orchestrator_gen6) for Person A - Flow 1: CV Upload & Local Text Extraction Flow.
Your working directory is: c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\orchestrator_gen6.
The original user request is recorded in: c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\ORIGINAL_REQUEST.md.

Please read `.agents/ORIGINAL_REQUEST.md` and initialize your `BRIEFING.md` and `plan.md` inside your working directory. Decompose the project into clear milestones and dispatch subagents to complete:
- R1: CV Resume Storage & Extraction Backend API (POST /api/applications/{id}/resume, GET /api/applications/{id}/resume, PDF/DOCX/OCR extraction, Zawgyi normalization via IMyanmarScriptNormalizer)
- R2: Bulk CV Upload Background Job (POST /api/jobpostings/{jobPostingId}/resumes/bulk, GET /api/jobpostings/{jobPostingId}/resumes/bulk/{batchId}, async background runner)
- R3: Candidate 360 SlideOver CV Viewer & Parsed Profile UI (CandidateSlideOver tab/panel with drag-and-drop & viewer, parsed profile human review confirmation panel, Bulk CV upload modal on JobPostingDetailPage)

Verification baselines to maintain:
- Backend tests: 333 tests passing baseline (dotnet test backend/RecruitOps.sln) + at least 8 new backend tests
- Frontend tests: 233 tests passing baseline (npm run test in frontend/internal) + at least 5 new frontend Vitest tests
- Typecheck: 0 errors (npm run typecheck)

Maintain Clean Architecture principles and full C# & TypeScript types alignment. Update progress.md as milestones complete. Claim victory to me (Sentinel) only when all requirements and acceptance criteria are fully met and verified.
