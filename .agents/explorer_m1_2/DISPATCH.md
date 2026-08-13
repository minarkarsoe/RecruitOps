## 2026-08-11T02:01:39Z
You are explorer_m1_2. Your working directory is c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\explorer_m1_2.
Read ORIGINAL_REQUEST.md at c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\ORIGINAL_REQUEST.md and PROJECT.md at c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\PROJECT.md.

Task: Provide the precise technical blueprint for Department Reach Scoping (ADR-0003 & ADR-0018) within SearchService.
Analyze:
1. How ICurrentUser and IDepartmentAccess are injected and used in SearchService.
2. Exact LINQ query filters for HiringManager (scoped to allowedDepartmentIds from IDepartmentAccess.GetAllowedDepartmentIdsAsync(ct)):
   - Requisitions: r.DepartmentId in allowedDeptIds.
   - JobPostings: p.DepartmentId in allowedDeptIds.
   - Candidates: candidates having job applications in allowedDeptIds or where user is an interview participant.
3. Exact LINQ query filters for Approver (ADR-0018):
   - Requisitions & Postings: company-wide (unscoped).
   - Candidates: strictly EXCLUDED unless candidate application has an interview where ApproverUserId == currentUser.UserId.
4. Exact LINQ query filters for Admin, HrDirector, Recruiter: unscoped across current tenant.

Write your report to c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\explorer_m1_2\analysis.md and handoff.md.
Send a message back to parent with summary and file path.
