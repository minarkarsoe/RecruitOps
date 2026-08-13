# Technical Blueprint: Department Reach Scoping (ADR-0003 & ADR-0018) in SearchService

## 1. Executive Summary

This document provides the precise technical blueprint for integrating **Department Reach Scoping (ADR-0003)** and **Candidate Data Exclusion for Approvers (ADR-0018)** into `SearchService` within `RecruitOps.Infrastructure`. 

`SearchService` executes full-text search queries across three entity categories: **Requisitions**, **JobPostings**, and **Candidates**. Because search results must never leak unauthorized data to users, `SearchService` must strictly enforce role scoping using EF Core LINQ query predicates before executing database searches.

---

## 2. Dependency Injection & Service Architecture

`SearchService` is registered as a scoped service in `RecruitOps.Infrastructure` and consumes `ICurrentUser` and `IDepartmentAccess`.

### Key Interface Dependencies
```csharp
namespace RecruitOps.Infrastructure.Services;

public class SearchService : ISearchService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IDepartmentAccess _departmentAccess;
    private readonly IMyanmarScriptNormalizer _normalizer;

    public SearchService(
        AppDbContext db,
        ICurrentUser user,
        IDepartmentAccess departmentAccess,
        IMyanmarScriptNormalizer normalizer)
    {
        _db = db;
        _user = user;
        _departmentAccess = departmentAccess;
        _normalizer = normalizer;
    }
}
```

### Usage of Injected Services
1. **`ICurrentUser`** (`backend/src/Application/Common/ICurrentUser.cs`):
   - `UserId` (`Guid?`): Identifies the active authenticated user ID. Used in interview participant subqueries.
   - `IsDepartmentScoped` (`bool`): `true` if role is `HiringManager` (`RoleScope.IsDepartmentScoped`). Governs requisition & job posting department filtering.
   - `IsExcludedFromCandidateData` (`bool`): `true` if role is `Approver` (`RoleScope.IsExcludedFromCandidateData`). Excludes candidate data by default unless user is an active interview panel participant.
2. **`IDepartmentAccess`** (`backend/src/Application/Common/IDepartmentAccess.cs`):
   - `AccessibleDepartmentIdsAsync(cancellationToken)`: Returns `Task<IReadOnlyCollection<Guid>>` containing department IDs assigned to the user in `UserDepartments`.

---

## 3. Scoping Matrix across Roles & Categories

| Role | Requisitions Scoping | JobPostings Scoping | Candidates Scoping |
| :--- | :--- | :--- | :--- |
| **Admin** | Unscoped (Company-wide) | Unscoped (Company-wide) | Unscoped (Company-wide) |
| **HrDirector** | Unscoped (Company-wide) | Unscoped (Company-wide) | Unscoped (Company-wide) |
| **Recruiter** | Unscoped (Company-wide) | Unscoped (Company-wide) | Unscoped (Company-wide) |
| **HiringManager** | Department Scoped (`allowedDeptIds`) | Department Scoped (`allowedDeptIds`) | Department Applications OR Interview Participation |
| **Approver** | Unscoped (Company-wide) | Unscoped (Company-wide) | **Strictly Excluded** (Interview Participation ONLY) |

---

## 4. Exact LINQ Query Filters

### 4.1 Scope Context Data Structure

To simplify scoping resolution during query composition, `SearchService` resolves a scope context struct:

```csharp
private record SearchScopeContext(
    bool IsUnauthenticated,
    bool IsExcludedFromCandidateData,
    bool IsDepartmentScoped,
    IReadOnlyCollection<Guid> AllowedDepartmentIds,
    Guid UserId
);

private async Task<SearchScopeContext> ResolveScopeContextAsync(CancellationToken ct)
{
    var userId = _user.UserId;
    if (userId is null)
    {
        return new SearchScopeContext(
            IsUnauthenticated: true,
            IsExcludedFromCandidateData: true,
            IsDepartmentScoped: true,
            AllowedDepartmentIds: Array.Empty<Guid>(),
            UserId: Guid.Empty
        );
    }

    var isExcluded = _user.IsExcludedFromCandidateData;
    var isDeptScoped = _user.IsDepartmentScoped;
    IReadOnlyCollection<Guid> allowedDeptIds = Array.Empty<Guid>();

    if (isDeptScoped)
    {
        allowedDeptIds = await _departmentAccess.AccessibleDepartmentIdsAsync(ct);
    }

    return new SearchScopeContext(
        IsUnauthenticated: false,
        IsExcludedFromCandidateData: isExcluded,
        IsDepartmentScoped: isDeptScoped,
        AllowedDepartmentIds: allowedDeptIds,
        UserId: userId.Value
    );
}
```

---

### 4.2 Requisitions Scoping Filter

Requisitions are linked to departments via `r.DepartmentId`.

```csharp
private IQueryable<Requisition> GetScopedRequisitionsQuery(SearchScopeContext scope)
{
    if (scope.IsUnauthenticated)
        return _db.Requisitions.AsNoTracking().Where(_ => false);

    var query = _db.Requisitions.AsNoTracking();

    // HiringManager: Filter by allowed department IDs
    if (scope.IsDepartmentScoped)
    {
        query = query.Where(r => scope.AllowedDepartmentIds.Contains(r.DepartmentId));
    }

    // Admin, HrDirector, Recruiter, Approver: Unscoped across current tenant
    return query;
}
```

---

### 4.3 JobPostings Scoping Filter

Job postings are linked to departments via `p.DepartmentId`.

```csharp
private IQueryable<JobPosting> GetScopedJobPostingsQuery(SearchScopeContext scope)
{
    if (scope.IsUnauthenticated)
        return _db.JobPostings.AsNoTracking().Where(_ => false);

    var query = _db.JobPostings.AsNoTracking();

    // HiringManager: Filter by allowed department IDs
    if (scope.IsDepartmentScoped)
    {
        query = query.Where(p => scope.AllowedDepartmentIds.Contains(p.DepartmentId));
    }

    // Admin, HrDirector, Recruiter, Approver: Unscoped across current tenant
    return query;
}
```

---

### 4.4 Candidates Scoping Filter

Candidates do not have a `DepartmentId` directly on the `Candidate` table. Candidate access is indirect via `JobApplication` (which references `JobPosting.DepartmentId`) or via `InterviewParticipant` (panel seat).

```csharp
private IQueryable<Candidate> GetScopedCandidatesQuery(SearchScopeContext scope)
{
    if (scope.IsUnauthenticated)
        return _db.Candidates.AsNoTracking().Where(_ => false);

    var query = _db.Candidates.AsNoTracking();

    if (scope.IsExcludedFromCandidateData)
    {
        // Approver (ADR-0018): Strictly EXCLUDED from candidate search
        // EXCEPTION: Candidate has an application with an interview where user is a participant
        query = query.Where(c => _db.JobApplications.Any(a =>
            a.CandidateId == c.Id &&
            _db.Interviews.Any(i =>
                i.JobApplicationId == a.Id &&
                _db.InterviewParticipants.Any(ip =>
                    ip.InterviewId == i.Id && ip.UserId == scope.UserId
                )
            )
        ));
    }
    else if (scope.IsDepartmentScoped)
    {
        // HiringManager (ADR-0003 & ADR-0017 §4):
        // Reached if candidate has application in allowed departments OR user is an interview participant
        query = query.Where(c => _db.JobApplications.Any(a =>
            a.CandidateId == c.Id &&
            (
                _db.JobPostings.Any(p =>
                    p.Id == a.JobPostingId &&
                    scope.AllowedDepartmentIds.Contains(p.DepartmentId)
                )
                ||
                _db.Interviews.Any(i =>
                    i.JobApplicationId == a.Id &&
                    _db.InterviewParticipants.Any(ip =>
                        ip.InterviewId == i.Id && ip.UserId == scope.UserId
                    )
                )
            )
        ));
    }

    // Admin, HrDirector, Recruiter: Unscoped across current tenant
    return query;
}
```

---

## 5. Trigram Full-Text Search Integration & Query Logic

Search query strings are first normalized using `_normalizer.Normalize(query)` to ensure Zawgyi Myanmar text is converted to Unicode NFC.

Matching uses PostgreSQL `pg_trgm` trigram search compatibility via `EF.Functions.ILike(field, $"%{normalizedQuery}%")`.

```csharp
// Example Candidate Search Execution:
var normalizedQuery = _normalizer.Normalize(rawQuery);
var pattern = $"%{normalizedQuery}%";

var candidateQuery = GetScopedCandidatesQuery(scope)
    .Where(c =>
        EF.Functions.ILike(c.FullName, pattern) ||
        (c.Email != null && EF.Functions.ILike(c.Email, pattern)) ||
        (c.Phone != null && EF.Functions.ILike(c.Phone, pattern)) ||
        _db.JobApplications.Any(a =>
            a.CandidateId == c.Id &&
            a.ResumeExtractedText != null &&
            EF.Functions.ILike(a.ResumeExtractedText, pattern)
        )
    );
```

---

## 6. Edge Cases & Verification Guardrails

1. **Unauthenticated Request (`_user.UserId == null`)**:
   Returns empty search results (`Where(_ => false)`) across all categories.
2. **HiringManager with 0 Allowed Departments**:
   `scope.AllowedDepartmentIds` is empty. Requisitions and JobPostings return 0 items. Candidate search only matches candidates where the user is an active interview panel participant.
3. **Approver User Role (`IsExcludedFromCandidateData == true`)**:
   `Approver` searches Requisitions and JobPostings across the entire company (unscoped). Candidate search returns 0 results unless the `Approver` is explicitly assigned to an interview panel (`InterviewParticipant`).
4. **Tenant Isolation (`ITenantScoped`)**:
   EF Core global query filters automatically enforce tenant boundary (`TenantId == currentTenantId`).
