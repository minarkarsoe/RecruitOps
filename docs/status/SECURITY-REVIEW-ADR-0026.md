# Security review — ADR-0026 step 4 and the delivery log

**Date:** 2026-08-26 · **Scope:** the two surfaces CLAUDE.md flagged as unreviewed —
ADR-0026 **step 4** (`BulkResumeService`, `BulkResumeWorker`, storage keys) and the
**delivery log** (`GET /api/delivery`) — plus the authorization and tenancy code they lean on.

**Result: one HIGH finding, confirmed against the running API and fixed in this change.**
Everything else examined held up.

---

## Vuln 1: Broken access control — an Approver could upload CVs into any posting and read the batch back

`backend/src/Infrastructure/Services/BulkResumeService.cs:81` (enqueue) and `:126` (status)

* **Severity:** HIGH
* **Category:** `broken_access_control` / `privilege_escalation`
* **Confidence:** 10 — reproduced against the real HTTP pipeline, not inferred.

### Description

Both entry points gated on `IDepartmentAccess.CanAccessAsync(posting.DepartmentId, ct)` **alone**.

That call answers *"does this role work across departments?"* — and an **Approver does**, by design:
ADR-0003 deliberately leaves `Approver` un-scoped so that a Finance approver can see the Sales
headcount request they have been asked to sign off. Asked about a **candidate**, that same `true`
grants everything.

ADR-0018 exists because of exactly this, one module earlier. `PipelineService` already carries the
corrected rule with the reasoning written out
(`PipelineService.cs:167`, `CanReachCandidatesInAsync`). `BulkResumeService` — written *after*
ADR-0018 was documented — did not.

The controller does not compensate: `JobPostingsController` is class-level
`[Authorize(Policy = Policies.InternalUser)]`, and both bulk endpoints inherit it. `InternalUser`
includes `Approver`. Its neighbours (`publish`, `close`, …) carry an explicit
`[Authorize(Policy = Policies.RecruitmentStaff)]`; the two bulk endpoints do not.

### Exploit scenario

Reproduced 2026-08-26 against the test API, as the seeded **Finance approver** against a **Sales**
posting they have no departmental relationship with:

```
POST /api/jobpostings/{sales-posting-id}/resumes/bulk     →  200 OK
{"batchId":"b8f8eafd-…","jobPostingId":"dababb80-…","totalFiles":1,"status":"Queued", …}
```

An authenticated Approver can therefore:

1. **Write** — inject up to 50 files per batch into any posting in the tenant. The worker turns
   each into a real `Candidate` and `JobApplication`, deduplicated against existing candidates. A
   role whose entire remit is approving headcount can put people into another department's
   pipeline, and the rows are indistinguishable from a recruiter's.
2. **Read** — `GET /api/jobpostings/{id}/resumes/bulk/{batchId}` returns per-file `FileName`,
   `CandidateId` and `ApplicationId`. CV filenames are very often the candidate's own name, so
   this is a candidate list by another route — the disclosure ADR-0018 was written to close.

The write half is the more serious: it is not a leak, it is data injected into someone else's
pipeline by a role that should reach no candidate at all.

### Fix (applied)

One door for both rules, mirroring `PipelineService`:

```csharp
private async Task<bool> CanReachCandidatesInAsync(Guid departmentId, CancellationToken ct)
    => !_currentUser.IsExcludedFromCandidateData
       && await _access.CanAccessAsync(departmentId, ct);
```

called from `EnqueueBatchAsync` and `GetBatchStatusAsync`. Both endpoints now return **404** — not
403 — so out-of-reach and non-existent stay indistinguishable, per the existing convention.

Regression tests live with their siblings in `ApproverReachTests`:
`An_Approver_Cannot_Bulk_Upload_CVs_Into_A_Posting` and
`An_Approver_Cannot_Read_A_Bulk_Upload_Batch`. Both were **proved against a mutation** — reverting
the guard to `CanAccessAsync` alone fails exactly those two and nothing else.

### The pattern worth acting on

This is the **third** instance of one mistake. Each service is asked to remember a two-part rule,
and the part that is easy to forget is the one that is not about departments. `IDepartmentAccess`
answers a question that is almost never the whole question for candidate data. Consider making the
candidate-facing helper the *only* exported way to ask — e.g. move `CanReachCandidatesInAsync` onto
`IDepartmentAccess` (or a `ICandidateReach`) and leave `CanAccessAsync` for the requisition axis —
so the next service physically cannot get half of it. Filed, not done here: it changes a shared
interface and belongs in its own change.

---

## Examined and clean

| Area | Finding |
|---|---|
| **SQL injection** | None possible — zero `FromSqlRaw` / `ExecuteSqlRaw` / interpolated SQL outside migrations, across all of `backend/src`. Search (`pg_trgm`) goes through EF. |
| **The delivery log** (`DeliveryLogService`) | Applies both rules: `IsExcludedFromCandidateData` first, then department reach, and **fails closed** on a subject it cannot resolve. 11 tests, all mutation-proved. No `PayloadJson` on the wire (a test asserts on the raw body, because the payload carries salary). |
| **Resume upload** (`ApplicationsController:52`) | Server-side extension allow-list and 10 MB cap — not just the client's. Authorized via `IApplicationAccess`, which applies ADR-0018. |
| **Resume download** (`:84`) | Keyed by `applicationId` + `IApplicationAccess`, never by a client-supplied storage key, so no IDOR or traversal. `File(stream, contentType, fileName)` sets `Content-Disposition: attachment`, so an uploader-chosen `Content-Type: text/html` cannot become stored XSS; the CSP (`default-src 'self'`, no `unsafe-inline`) is a second layer. |
| **Tenant resolution** (`CurrentTenant`) | Request claim is read **first and wins**, ambient second. Nothing reachable from an authenticated request can redirect it at another tenant. `AmbientTenantScope` refuses a second `EnterTenant`, so a recycled scope throws rather than crossing tenants. |
| **Both background workers** | ADR-0026 §4 followed exactly: `IgnoreQueryFilters()` on the claim and the bookkeeping write only, a fresh DI scope per item, tenant entered from the claimed row, and a re-read inside the scope that fails loudly if the row is not readable. No hand-written tenant predicates in handler code. |
| **Refresh tokens** (`AuthService.RefreshTokenAsync`) | Textbook rotation with reuse detection — a replayed revoked token revokes the whole family. Expiry and `User.IsActive` both checked. `IgnoreQueryFilters()` is correct here: the request is unauthenticated, and the lookup key is the unguessable token itself. |
| **Permission handler** | Database is authoritative and **denied is final**; the claim-based fallback that once topped users up to Recruiter's seeded set is gone. Super-admin bypass is explicit and logged. |
| **Login** | Dummy-hash verification on unknown accounts (no user-enumeration timing signal), two-axis throttling (ADR-0016), empty 401 body. |
| **Storage keys** | `Path.GetExtension` can never return a directory separator, so the bulk key is traversal-proof. `ResumeService` interpolates the raw filename — see the observation below. |

---

## Observations (not vulnerabilities)

**`ResumeService.cs:68` interpolates the raw client filename into the object key.**

```csharp
string fileKey = $"applications/{applicationId}/resume/{Guid.NewGuid()}_{file.FileName}";
```

Not exploitable: S3/MinIO treat keys as literal strings rather than resolving `..`, the server-side
extension allow-list runs first, and the `Guid.NewGuid()` prefix makes collisions impossible. It is
the only place in the codebase where an unsanitised user string reaches a storage key, and the
neighbouring `BulkResumeService` does it properly (`{row.Id}{extension}`) — worth aligning the next
time the file is open, on tidiness grounds rather than security ones.

**`X-Tenant-Id` is sent by the SPA and ignored by the API.** `frontend/internal/src/lib/api.ts`
attaches it whenever `session.activeTenantId` is set, and no backend code reads it. Good news
security-wise — there is no header-based tenant override to abuse — but it means the super-admin
tenant switcher **does not actually switch tenants**. That is a functional gap, filed here only
because it looks like a security control and is not one.

> **Resolved 2026-08-26 — and it is now a security control, so read this.** The header is honoured,
> **only** for a caller whose signed token carries `is_super_admin`; `CurrentTenant` resolves it
> ahead of the tenant claim for that one case and ignores it entirely for everyone else. The old
> guarantee ("the claim always wins, so no authenticated request can be redirected") no longer
> holds as written. The replacement is: **a request can be redirected at another company's data if
> and only if the token says super-admin.**
>
> If you are reviewing a change to `CurrentTenant`, `CurrentUser.IsSuperAdminPrincipal`, or
> `SuperAdminTenantOverrideMiddleware`, treat it as tenant-isolation code. `CurrentTenantResolutionTests`
> and `TenantOverrideTests` pin both directions; removing the gate fails four of them, which was
> verified by mutation rather than assumed.

**`Roles.cs` is missing `Interviewer`**, though its own comment says it "must match
`RecruitOps.Domain.Enums.UserRole`" and `RbacSeedData` seeds that role. The practical effect today
is that `Interviewer` appears in no policy and so reaches no policy-gated endpoint — which is
conservative, i.e. failing in the safe direction, but by omission rather than by decision.
