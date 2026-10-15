# Handoff Report: Backend Search Service Technical Blueprint (M1)

## 1. Observation
- **Project Baseline**: Executed `dotnet test backend/RecruitOps.sln`. Output: `Passed! - Failed: 0, Passed: 51, Skipped: 0` for `RecruitOps.Domain.Tests.dll` and `Passed! - Failed: 0, Passed: 336, Skipped: 0` for `RecruitOps.Api.Tests.dll` (total 387 passing tests).
- **Existing Entities & Structure**:
  - `Candidate` (`backend/src/Domain/Entities/Candidate.cs`): `FullName`, `Email`, `Phone`, `TenantId`.
  - `JobApplication` (`backend/src/Domain/Entities/JobApplication.cs`): `ResumeExtractedText`, `CoverNote`, `CustomFieldsJson`, `JobPostingId`, `CandidateId`.
  - `JobPosting` (`backend/src/Domain/Entities/JobPosting.cs`): `Title`, `Description`, `Location`, `ApplicationFormFieldsJson`, `DepartmentId`.
  - `Requisition` (`backend/src/Domain/Entities/Requisition.cs`): `Title`, `JobDescription`, `DepartmentId`.
- **Existing Normalizer & Access Controls**:
  - `IMyanmarScriptNormalizer` (`backend/src/Application/Interfaces/IMyanmarScriptNormalizer.cs`): Method `Normalize(string? input)` returning `MyanmarScriptNormalizationResult` (with implicit `string` conversion). Registered as singleton.
  - `IDepartmentAccess` (`backend/src/Application/Common/IDepartmentAccess.cs`): `AccessibleDepartmentIdsAsync(ct)` and `CanAccessAsync(departmentId, ct)`.
  - `ICurrentUser` (`backend/src/Application/Common/ICurrentUser.cs`): `IsDepartmentScoped` and `IsExcludedFromCandidateData`.
- **Target File Paths**:
  - `backend/src/Application/DTOs/Search/SearchDtos.cs`
  - `backend/src/Application/Interfaces/ISearchService.cs`
  - `backend/src/Infrastructure/Services/SearchService.cs`

---

## 2. Logic Chain
1. **Observation 1 & DTO Requirements**: `PROJECT.md` specifies the search API contract with query string, normalized query, category filter, total match count, category count breakdown (`all`, `candidates`, `postings`, `requisitions`), and paginated item list with snippets, relevance scores, and target URLs.
   - **Inference**: Defining 4 immutable record types (`SearchQueryParameters`, `SearchResultItemDto`, `CategoryCountsDto`, `SearchResponseDto`) in `RecruitOps.Application.DTOs.Search` fulfills this contract cleanly while maintaining Clean Architecture isolation.

2. **Observation 2 & Interface Requirements**: `ISearchService` needs to be defined in `RecruitOps.Application.Interfaces` taking `SearchQueryParameters` and `CancellationToken`.
   - **Inference**: Method signature `Task<SearchResponseDto> SearchAsync(SearchQueryParameters queryParams, CancellationToken ct = default);` exposes a clean, strongly-typed interface for `SearchController`.

3. **Observation 3 & Search Engine Architecture**:
   - Query input must undergo Zawgyi-to-Unicode normalization via `IMyanmarScriptNormalizer.Normalize()` before executing database query.
   - Security constraints must be enforced prior to returning data: `_user.IsExcludedFromCandidateData` restricts candidate data access (ADR-0018), while `_user.IsDepartmentScoped` restricts candidate, job posting, and requisition results to allowed department IDs from `_access.AccessibleDepartmentIdsAsync(ct)` (ADR-0003).
   - Text matching across `Candidate` (name, email, phone, extracted CV text, cover note, custom fields), `JobPosting` (title, description, location, custom form fields), and `Requisition` (title, job description) using `EF.Functions.ILike` ensures `pg_trgm` PostgreSQL compatibility while remaining testable in EF Core In-Memory database.
   - Relevance score calculation maps field priorities to a 0.0-100.0 scale, with exact title matches receiving top priority (100.0) down to JSON field matches (45.0), plus frequency bonuses.
   - Context snippet extraction uses a centered window around match position (~180 chars), HTML-encodes special characters, and wraps matched search terms in `<mark>` tags.

---

## 3. Caveats
- **In-Memory Score Evaluation**: In EF Core, string manipulation and complex scoring functions are evaluated in-memory post-query. Database query filters should first fetch matching candidate/posting/requisition rows via `ILike` / `.Contains()`, followed by in-memory snippet extraction and relevance score evaluation.
- **Candidate Multi-Application Deduplication**: A candidate may have multiple applications. The search query should group by candidate ID or pick the candidate's latest application context to avoid duplicate candidate cards in search results.

---

## 4. Conclusion
The technical blueprint for `SearchDtos.cs`, `ISearchService.cs`, and `SearchService.cs` is complete, detailed, and fully specified in `analysis.md`. The design aligns 100% with Clean Architecture, ADR-0003 department scoping, ADR-0009 Myanmar script handling, and ADR-0018 candidate data privacy guidelines.

---

## 5. Verification Method
1. **Source Inspection**: Verify that created files exist at exact paths:
   - `backend/src/Application/DTOs/Search/SearchDtos.cs`
   - `backend/src/Application/Interfaces/ISearchService.cs`
   - `backend/src/Infrastructure/Services/SearchService.cs`
2. **Build Verification**: Run `dotnet build backend/RecruitOps.sln` to confirm zero compilation errors.
3. **Test Suite Verification**: Run `dotnet test backend/RecruitOps.sln` to verify all existing 387 unit/integration tests pass cleanly.
4. **Invalidation Conditions**: Any failure in `dotnet build` or regression in existing 387 backend unit tests invalidates the implementation.
