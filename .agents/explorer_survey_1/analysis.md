# Backend Codebase Survey Report: Person B - Flow 1 (Full-text Search API & Scoping)

## 1. Executive Summary & Architecture Overview

The RecruitOps backend is built on **.NET 10 LTS** adhering strictly to **Clean Architecture** principles. The solution (`backend/RecruitOps.sln`) comprises 4 main src projects and 2 test projects:

- **`RecruitOps.Domain`** (`backend/src/Domain`): Domain entities (`Candidate`, `JobApplication`, `JobPosting`, `Requisition`, `Department`, `User`, `UserDepartment`), Enums (`UserRole`, `PipelineStatus`, `JobStatus`, `RequisitionStatus`), Base classes (`BaseEntity`, `ITenantScoped`), and domain scope rules (`RoleScope.cs`).
- **`RecruitOps.Application`** (`backend/src/Application`): Application DTOs, Service Interfaces (`IMyanmarScriptNormalizer`, `ISearchService` [to be added], `IJobPostingService`, `IRequisitionService`), and Security/Context interfaces (`ICurrentUser`, `ICurrentTenant`, `IDepartmentAccess`, `IApplicationAccess`).
- **`RecruitOps.Infrastructure`** (`backend/src/Infrastructure`): EF Core `AppDbContext`, EF Migrations, Service implementations (`MyanmarScriptNormalizer`, `DepartmentAccess`, `ApplicationAccess`, `S3FileStorage`, `DocumentTextExtractor`, `AnalyticsService`), Options, and `DependencyInjection.cs`.
- **`RecruitOps.Api`** (`backend/src/Api`): ASP.NET Core controllers (`JobPostingsController`, `RequisitionsController`, `AnalyticsController`, `CandidatesController`), Auth/Permission Policies (`Policies.InternalUser`, `HasPermission`), and `Program.cs`.

---

## 2. Existing Entities & Fields for Search

### 2.1 Candidate & JobApplication
- **`Candidate`** (`backend/src/Domain/Entities/Candidate.cs`):
  - `Id` (`Guid`)
  - `FullName` (`string`, max 200)
  - `Email` (`string?`, max 256)
  - `Phone` (`string?`, max 30)
  - `Source` (`SourceChannel` enum)
- **`JobApplication`** (`backend/src/Domain/Entities/JobApplication.cs`):
  - `CandidateId` (`Guid`)
  - `JobPostingId` (`Guid`)
  - `ResumeExtractedText` (`string?`, contains normalized CV plain text)
  - `CoverNote` (`string?`, max 4000)
  - `CustomFieldsJson` (`string?`, JSONB application responses)
  - `IsZawgyiNormalized` (`bool`)

*Search Scope for Candidates*: Matches across `Candidate.FullName`, `Candidate.Email`, `Candidate.Phone`, and `JobApplication.ResumeExtractedText`, `JobApplication.CoverNote`, `JobApplication.CustomFieldsJson`.

### 2.2 JobPosting
- **`JobPosting`** (`backend/src/Domain/Entities/JobPosting.cs`):
  - `Id` (`Guid`)
  - `DepartmentId` (`Guid`, FK to `Department`)
  - `RequisitionId` (`Guid`, FK to `Requisition`)
  - `Title` (`string`, max 200)
  - `Description` (`string`)
  - `Location` (`string?`, max 200)
  - `EmploymentType` (`EmploymentType` enum)
  - `ApplicationFormFieldsJson` (`string?`, JSONB schema for custom form questions)
  - `Status` (`JobStatus` enum: `Draft`, `Published`, `Closed`, `Archived`)

*Search Scope for Job Postings*: Matches across `Title`, `Description`, `Location`, `EmploymentType` string representation, `ApplicationFormFieldsJson`, and joined `Department.Name`.

### 2.3 Requisition
- **`Requisition`** (`backend/src/Domain/Entities/Requisition.cs`):
  - `Id` (`Guid`)
  - `DepartmentId` (`Guid`, FK to `Department`)
  - `RequestedByUserId` (`Guid`)
  - `Title` (`string`, max 200)
  - `JobDescription` (`string`)
  - `Status` (`RequisitionStatus` enum)

*Search Scope for Requisitions*: Matches across `Title`, `JobDescription`, joined `Department.Name`, and `Department.Code` / Requisition ID string.

---

## 3. Existing `IMyanmarScriptNormalizer` Service & Integration

- **Interface**: `RecruitOps.Application.Interfaces.IMyanmarScriptNormalizer`
- **Implementation**: `RecruitOps.Infrastructure.Services.MyanmarScript.MyanmarScriptNormalizer`
- **DI Registration**: Registered as `AddSingleton<IMyanmarScriptNormalizer, MyanmarScriptNormalizer>()` in `Infrastructure/DependencyInjection.cs:100`.
- **Key Methods**:
  - `Normalize(string? input)` -> returns `MyanmarScriptNormalizationResult` containing `NormalizedText` (Unicode FormC), `IsZawgyiDetected`, `ConfidenceScore`, `DetectedEncoding`.
  - `IsZawgyi(string? input)` -> returns `bool`.

### Integration for Search:
When a query parameter `q` arrives at `GET /api/search?q={query}`, the search service passes `query` to `_normalizer.Normalize(query).NormalizedText`. Because all stored CV text and names in the database were normalized on ingestion (per ADR-0009), normalizing the input search query guarantees that Zawgyi queries match Unicode stored records seamlessly.

---

## 4. Database Context / EF Core & Trigram (`pg_trgm`) Index Feasibility

### 4.1 Production Database Setup (PostgreSQL)
In `Infrastructure/DependencyInjection.cs`, production uses `Npgsql.EntityFrameworkCore.PostgreSQL`.
- PostgreSQL supports the `pg_trgm` extension for fast GIN/GiST trigram indexes on text fields.
- Migration statement: `migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");`
- Trigram GIN indexes can be created on `Candidate.FullName`, `JobApplication.ResumeExtractedText`, `JobPosting.Title`, `Requisition.Title`.

### 4.2 EF Core InMemory Test Provider Compatibility
- Integration tests in `tests/RecruitOps.Api.Tests` use `CustomWebAppFactory.cs`, which replaces Npgsql with EF Core `UseInMemoryDatabase`.
- **Constraint**: `UseInMemoryDatabase` does **NOT** support PostgreSQL raw SQL (`FromSqlRaw`, `%` trigram operators, or Postgres-specific functions). Using raw Postgres SQL breaks all 387 existing backend unit/integration tests!
- **Optimal Approach**: Use EF Core LINQ methods with `EF.Functions.Like(x.Field, $"%{normalizedQuery}%")` or `EF.Functions.ILike(...)`.
  - In PostgreSQL, `EF.Functions.ILike` / `EF.Functions.Like` with `%pattern%` automatically utilizes the GIN `pg_trgm` index on Postgres tables.
  - In EF Core InMemory Test Provider, `EF.Functions.Like` translates cleanly in-memory, ensuring 100% test compatibility and high performance.

---

## 5. Department Reach Scoping (ADR-0003 & ADR-0018) Implementation Strategy

Department reach scoping is enforced explicitly at the application service layer via `IDepartmentAccess` and `ICurrentUser`:

- **Role Classifications (`RoleScope.cs`)**:
  - `UserRole.HiringManager`: `IsDepartmentScoped` is `true`. Scoped to departments in `UserDepartments`.
  - `UserRole.Approver`: `IsExcludedFromCandidateData` is `true` (ADR-0018). Sees requisitions/postings unscoped, but cannot see candidates unless listed on an interview panel (`InterviewParticipants`).
  - `UserRole.Admin`, `UserRole.HrDirector`, `UserRole.Recruiter`: Unscoped (`IsDepartmentScoped` is `false`, `IsExcludedFromCandidateData` is `false`).

### 5.1 Scoping Rules for Search Categories:
1. **Requisitions Category**:
   - Unscoped roles: Query all requisitions in current tenant.
   - `HiringManager`: Filter by `allowedDepartmentIds.Contains(r.DepartmentId)`.
2. **Job Postings Category**:
   - Unscoped roles: Query all postings in current tenant.
   - `HiringManager`: Filter by `allowedDepartmentIds.Contains(p.DepartmentId)`.
3. **Candidates Category**:
   - `Admin`, `HrDirector`, `Recruiter`: Query all candidates in tenant.
   - `Approver`: Excluded from candidates unless candidate's application has an interview where `ApproverUserId == currentUser.UserId`.
   - `HiringManager`: Filter candidates to those having an application in `allowedDepartmentIds` OR where the manager is an interview panel participant for that application.

---

## 6. Existing Backend Test Suite Baseline & Search Test Structure

- **Current Suite Baseline**: **387 tests passing** (51 in `RecruitOps.Domain.Tests` + 336 in `RecruitOps.Api.Tests`).
- **Test Infrastructure**: `CustomWebAppFactory.cs` seeds `TenantA`, `TenantB`, `SalesDepartmentId`, `FinanceDepartmentId`, `HiringManagerUserId` (Sales only), `FinanceManagerUserId` (Finance only), `AdminUserId`, `FinanceApproverUserId`.

### Recommended Search Test Structure (`SearchApiTests.cs`):
- `Search_WithValidQuery_ReturnsRankedResultsAcrossCategories`
- `Search_WithZawgyiQuery_NormalizesToUnicodeAndMatches`
- `Search_WithCategoryFilter_FiltersToSpecificCategory`
- `Search_CandidateCvText_MatchesResumeExtractedText`
- `Search_HiringManager_EnforcesDepartmentReachScoping`
- `Search_Approver_ExcludesCandidateDataUnlessOnPanel`
- `Search_TenantIsolation_DoesNotReturnOtherTenantResults`
- `Search_NoMatches_ReturnsEmptyResultLists`

---

## 7. Proposed API Interface & Contract

- **Endpoint**: `GET /api/search?q={query}&category={category}`
- **Authorization**: `[Authorize(Policy = Policies.InternalUser)]`
- **Query Parameters**:
  - `q` (`string`, required): Search keyword (English or Zawgyi/Unicode Burmese).
  - `category` (`string?`, optional): `All` (default), `Candidates`, `Postings`, `Requisitions`.
- **Response DTO (`SearchResultDto`)**:
  ```csharp
  public record SearchResultDto(
      IReadOnlyList<CandidateSearchItemDto> Candidates,
      IReadOnlyList<JobPostingSearchItemDto> JobPostings,
      IReadOnlyList<RequisitionSearchItemDto> Requisitions,
      int TotalMatches
  );
  ```
