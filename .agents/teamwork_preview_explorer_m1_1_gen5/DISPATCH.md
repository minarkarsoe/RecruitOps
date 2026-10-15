## 2026-08-06T13:14:33Z
You are an Explorer subagent (teamwork_preview_explorer_m1_1_gen5). Your working directory is `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_explorer_m1_1_gen5`.

Please read:
1. `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\ORIGINAL_REQUEST.md`
2. `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\RecruitOps_Design_System.md`
3. `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\orchestrator_gen5\PROJECT.md`
4. `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_explorer_survey_2_gen5\handoff.md`

Your task:
Analyze Milestone 1 (Design System Polish & Signature Components) and produce a step-by-step implementation plan for the Worker:
1. Fonts & Line-height: Update `frontend/internal/src/index.css` and `frontend/public/app/globals.css` body styles to `line-height: 1.7;` (Burmese-safe). Add Google Fonts link in `frontend/public/app/layout.tsx`.
2. `StatusPill` Vocabulary Extension (`packages/ui/src/StatusPill.tsx`): Add support for `Sent to Client` (info), `Placed` (success), `Accepted` (success), `Need More Info` (warning), `Active` (success), `Expiring Soon` (warning), `Expired` (danger).
3. Signature Components:
   - `PipelineStageRail` (`packages/ui/src/PipelineStageRail.tsx`): Horizontal stage count rail (`Sourced 24 -> Shortlisted 8 -> Sent 5 -> Interview 2 -> Placed 1`) with mono numbers and status pill colors.
   - `ExpiryAttentionCard` (`packages/ui/src/ExpiryAttentionCard.tsx`): Dashboard card listing expiring contracts/jobs with urgency color-coded mono countdowns (>30d ink, 8-30d warning, <=7d danger) and "Renew" action button.
   - `ClientPortalCard` & `ClientFeedbackBar` (`packages/ui/src/ClientPortalCard.tsx`): Premium candidate card layout (avatar, name, role, quiet chips, CV button) + full-width 44px height feedback bar (`Accept for Interview`, `Need More Info`, `Reject`).
4. Re-export components in `packages/ui/src/index.ts`.
5. Define component unit test suites in `frontend/internal/src/components/ui/` or `packages/ui`.

Write your implementation plan and handoff to `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_explorer_m1_1_gen5\handoff.md`.
Send a completion message when done.
