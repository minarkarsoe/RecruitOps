## 2026-08-08T07:57:26Z

You are the Project Orchestrator for RecruitOps.

Workspace: c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps
Working directory for agent files: c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\orchestrator_gen7

Your task is to execute Person A - Flow 1 (Milestone 2 & Milestone 3):
- Milestone 1 is already COMPLETE and PASSING all 349 backend tests.

Remaining Work:
### R2. Bulk CV Upload Background Job (Milestone 2)
- Endpoint `POST /api/jobpostings/{jobPostingId}/resumes/bulk` to accept up to 50 CV files in a single batch.
- Process files asynchronously using background job runner without blocking HTTP requests.
- Track per-file processing status (`Queued`, `Processing`, `Success`, `Skipped`, `Failed`) with progress summary endpoint `GET /api/jobpostings/{jobPostingId}/resumes/bulk/{batchId}`.

### R3. Candidate 360 SlideOver CV Viewer & Parsed Profile UI (Milestone 3)
- Update `CandidateSlideOver.tsx` in `@recruitops/internal`:
  - Add "CV & Documents" tab/section with drag-and-drop upload zone, upload progress bar, and embedded CV text viewer / download button.
  - Add "Parsed Profile Human Review" panel that shows extracted text side-by-side with editable candidate profile fields (Name, Email, Phone, Experience, Skills), requiring explicit recruiter confirmation before applying changes to candidate profile.
- Add Bulk CV Upload modal on `JobPostingDetailPage` allowing recruiters to drag-and-drop multiple CVs with live progress indicators per file.

## Acceptance Criteria
- [ ] Bulk upload endpoint `POST /api/jobpostings/{jobPostingId}/resumes/bulk` accepts up to 50 files and returns batch tracking ID
- [ ] Batch progress endpoint `GET /api/jobpostings/{jobPostingId}/resumes/bulk/{batchId}` returns per-file status summary
- [ ] `CandidateSlideOver.tsx` displays CV upload zone, file preview link, and extracted text viewer
- [ ] Recruiter can edit and confirm parsed profile data before updating candidate records
- [ ] Bulk upload modal on `JobPostingDetailPage` displays progress bar per file
- [ ] All 349+ existing backend tests pass cleanly (`dotnet test backend/RecruitOps.sln`)
- [ ] All 233+ existing frontend tests pass cleanly (`npm run test` in `frontend/internal`)
- [ ] `npm run typecheck` passes with 0 errors across all workspaces

Refer to `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\ORIGINAL_REQUEST.md` for full specification.
Maintain progress in `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\orchestrator_gen7\progress.md`.
When all work is verified and complete, message the Sentinel parent with your victory claim.
