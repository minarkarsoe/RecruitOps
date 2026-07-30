# Requirement R4 — End-to-End API Integration Test Results

**Date**: 2026-07-29  
**Test Target**: `FullUserJourneyIntegrationTests.cs`  
**Framework**: xUnit + Microsoft.AspNetCore.Mvc.Testing (CustomWebAppFactory)  
**Database Provider**: EF Core In-Memory (Isolated per factory instance)

---

## 1. Test Suite Summary

- **Total Solution Tests**: 172
  - `RecruitOps.Domain.Tests`: 39 Passed, 0 Failed
  - `RecruitOps.Api.Tests`: 133 Passed, 0 Failed
- **FullUserJourneyIntegrationTests Suite**: 3 Passed, 0 Failed (100% pass rate)

---

## 2. Multi-Module Connected Journey Breakdown (Steps 1–9)

### Step 1: Admin Setup & Department Management
- **Action**: Admin creates department `Engineering E2E` (`POST /api/departments`) and assigns HiringManager & Admin as department members (`PUT /api/departments/{id}/members`).
- **Assertion**: HTTP 201 Created returned. Roster query (`GET /api/departments/{id}/members`) confirms `IsMember == true` for assigned user IDs.

### Step 2: Requisition Creation & Submission
- **Action**: Hiring Manager creates requisition `Lead Software Architect` (`POST /api/requisitions`) in `Draft` state and submits for approval (`POST /api/requisitions/{id}/submit`).
- **Assertion**: Requisition transitions from `Draft` to `PendingApproval` awaiting initial `HR` step.

### Step 3: Sequential Approval Enforcement
- **Action**: 
  1. Finance Approver (Step 2) attempts to approve prematurely.
  2. HR / Admin (Step 1) approves requisition (`POST /api/requisitions/{id}/decision`).
  3. Finance Approver (Step 2) approves requisition.
- **Assertion**: Queue-jumping attempt returns HTTP 404 NotFound. Step 1 approval moves status to `PendingApproval` awaiting `Finance`. Step 2 approval completes the chain, transitioning requisition status to `Approved`.

### Step 4: Job Posting Creation & Publishing
- **Action**: Recruiter creates job posting from approved requisition (`POST /api/jobpostings`), attaches custom application form schema (`PUT /api/jobpostings/{id}`), and publishes posting (`POST /api/jobpostings/{id}/publish`).
- **Assertion**: Draft posting is generated from approved requisition. Custom schema containing `github_url` and `years_exp` fields is validated. Publishing mints a cryptographically strong `PublicToken` and sets status to `Live`.

### Step 5: Public Anonymous Application Submission
- **Action**: Unauthenticated public applicant views public job page (`GET /api/public/jobs/{token}`) and submits application with custom field answers (`POST /api/public/jobs/{token}/apply`).
- **Assertion**: Job page details match public properties. Application submission succeeds with custom JSON answers validated and normalized.

### Step 6: Candidate Deduplication
- **Action**: Second application submitted using matching candidate email (`kyaw.kyaw@example.com`) and alternate phone format (`09123456789` vs `+95 9 123 456 789`).
- **Assertion**: Pipeline query (`GET /api/jobpostings/{id}/pipeline`) confirms 2 pipeline items sharing the exact same `CandidateId` entity. Phone and email normalization functions correctly.

### Step 7: Pipeline Stage Advance & Interview Scheduling
- **Action**: Recruiter advances candidate stage to `Interview` (`POST /api/applications/{id}/stage`) and schedules Round 1 interview with panel members (`POST /api/applications/{id}/interviews`).
- **Assertion**: Stage moves to `Interview`. Interview record is created with `Round = 1`, `Status = "Scheduled"`, and assigned panel members (Sales Manager & Finance Manager).

### Step 8: Blind Scorecard Evaluation & Collaborative Notes
- **Action**:
  1. Finance Manager submits scorecard (`POST /api/interviews/{id}/scorecard/submit`).
  2. Sales Manager queries panel scorecards (`GET /api/interviews/{id}/scorecards`) before submitting own scorecard.
  3. Sales Manager submits own scorecard.
  4. Sales Manager re-queries panel scorecards.
  5. Recruiter adds collaborative note with `@sales.manager` handle (`POST /api/applications/{id}/notes`).
- **Assertion**:
  - Before submission: `BlindedUntilYouSubmit == true`, `Visible` scorecards count is 0, `HiddenCount == 1`.
  - After submission: `BlindedUntilYouSubmit == false`, `Visible` scorecards count is 2 (unblinded).
  - Note creation parses mention: `Mentions[0].DisplayName == "Sales Manager"`, `BodyHtml` contains `class="mention"`.

### Step 9: Stage History Timeline Verification
- **Action**: Fetch stage history timeline (`GET /api/applications/{id}/history`).
- **Assertion**:
  - Entry 0: `FromStatus = null`, `ToStatus = "Applied"`, `ChangedByName = null` (anonymous submission).
  - Entry 1: `FromStatus = "Applied"`, `ToStatus = "Interview"`, `ChangedByName = "Alpha Admin"`, note contains transition rationale.
  - Chronological ordering holds (`firstEntry.ChangedAt <= secondEntry.ChangedAt`).

---

## 3. Test Execution Logs

```
Test run for RecruitOps.Api.Tests.dll (.NETCoreApp,Version=v10.0)
Passed!  - Failed: 0, Passed: 133, Skipped: 0, Total: 133, Duration: 4 s

Test run for RecruitOps.Domain.Tests.dll (.NETCoreApp,Version=v10.0)
Passed!  - Failed: 0, Passed: 39, Skipped: 0, Total: 39, Duration: 112 ms
```
