## 2026-08-03T10:49:48Z

You are Worker 2 (App Layout & Global Navigation).
Working Directory: c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\worker_m2
Original Request Path: c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\ORIGINAL_REQUEST.md
Project Scope Path: c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\PROJECT.md
Survey Analysis Path: c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\explorer_survey_2\analysis.md

MANDATORY INTEGRITY WARNING:
DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A teamwork_preview_auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.

File Ownership:
You have exclusive write access to:
- frontend/internal/src/components/AppLayout.tsx
- frontend/internal/src/components/Header.tsx
- frontend/internal/src/components/Sidebar.tsx
- frontend/internal/src/components/Breadcrumbs.tsx
- frontend/internal/src/components/TenantSwitcherBar.tsx
- frontend/internal/src/components/AppLayout.test.tsx

Task Description:
Implement Milestone 2 (Application Layout & Global Navigation):
1. Redesign `AppLayout.tsx` to incorporate a high-density, modern CRM shell:
   - Modern collateral sidebar (`Sidebar.tsx`) with grouped navigation links (Recruitment, Team, Governance).
   - Top Header (`Header.tsx`) with dynamic route-based Breadcrumbs (`Breadcrumbs.tsx`), department/user profile info, and search button with `Ctrl+K` shortcut indicator badge.
   - Global `Ctrl+K` / `Cmd+K` keyboard event listener that opens/closes the `CommandPalette` modal primitive from `@recruitops/ui`.
   - Preserve `TenantSwitcherBar` context bar for SuperAdmin users.
   - Permission-aware navigation link rendering using `hasPermission(session, code)`.
2. Ensure backward compatibility with existing tests in `AppLayout.test.tsx`:
   - Keep all navigation link labels ("Requisitions", "Job postings", "Inbox", "Users", "Role Builder", etc.) so existing query selectors match.
3. Add new unit tests to `AppLayout.test.tsx` to verify:
   - `Ctrl+K` keyboard shortcut opens the Command Palette overlay.
   - Breadcrumbs update dynamically based on the current location route.
   - Command palette allows searching and route navigation.
4. Run `npm run typecheck` across workspaces and `npm run test` in frontend/internal. Ensure 0 TypeScript errors and all tests pass.
5. Write a detailed handoff report to c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\worker_m2\handoff.md documenting changes and test command results. Send a message to parent when done.
