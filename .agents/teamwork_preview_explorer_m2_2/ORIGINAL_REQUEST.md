## 2026-07-29T16:25:02Z
You are Explorer 2 for Milestone 2 of RecruitOps.
Your working directory is: c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_explorer_m2_2
Project root: c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps

Objective:
Design the granular permission taxonomy and seed matrix for Requirement R2:
1. Define standard string permission codes formatted as `permission:<module>:<feature>:<action>`:
   - Modules: Requisitions, Postings, Applications, Interviews, Scorecards, Users, Roles, Settings.
   - Standard CRUD: Create, Read, Update, Delete.
   - Special Actions: Approve, Publish, Cancel, BlindEvaluation.
2. Define the pre-configured role-permission mappings for:
   - `SuperAdmin`: All permissions + cross-tenant management.
   - `Admin`: Full tenant permissions.
   - `HrDirector`: Requisitions (CRUD, Approve), Postings (CRUD, Publish), Applications, Interviews, Scorecards, Users (Read/Assign).
   - `Recruiter`: Requisitions (Create, Read), Postings (CRUD, Publish), Applications (CRUD), Interviews (Schedule, Cancel), Scorecards.
   - `HiringManager`: Requisitions (Create, Edit), Applications (Read), Interviews (Read, Schedule), Scorecards.
   - `Approver`: Requisitions (Read, Approve), Applications (Read).
   - `Interviewer`: Interviews (Read), Scorecards (Submit, BlindEvaluation).
3. Document how seed data will initialize these default roles and permissions in EF Core.

Output:
Write report to `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\teamwork_preview_explorer_m2_2\analysis.md` and `handoff.md`. Send a message when finished.
