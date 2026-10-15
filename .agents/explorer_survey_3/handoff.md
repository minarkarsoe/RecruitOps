# Handoff Report: Person B - Flow 1 Specification Survey

**Agent**: explorer_survey_3  
**Working Directory**: `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\explorer_survey_3`  
**Handoff Type**: Hard (Task Complete)

---

## 1. Observation

1. **ADRs and Authorization Rules**:
   - `docs/decisions/ADR-0003-department-scoping.md` (lines 22–27, 38–41): `HiringManager` sees only data belonging to their own department (`Requisition`, `JobPosting`, `JobApplication`, `Candidate`). Scoping is enforced explicitly in the application layer via `IDepartmentAccess` / `ApplicationAccess` rather than EF Core global query filters.
   - `docs/decisions/ADR-0018-approver-candidate-data-exclusion.md` & `backend/src/Domain/RoleScope.cs` (lines 28–42): `Approver` role is company-wide on requisitions axis, BUT `IsExcludedFromCandidateData` is `true`. Approvers have no standing reach into candidate data (candidates, applications, scorecards, notes).
   - `docs/decisions/ADR-0009-myanmar-script-handling.md` (lines 31–39, 73–82): All text entry points (and search queries) must convert Zawgyi → Unicode NFC at the boundary before processing. PostgreSQL default FTS tokenisation does not support Burmese without spaces; trigram indexing (`pg_trgm`) over normalized Unicode is specified.

2. **Existing Infrastructure & Codebase**:
   - `backend/src/Infrastructure/Services/MyanmarScript/MyanmarScriptNormalizer.cs` (lines 109–152): Injectable `IMyanmarScriptNormalizer` service already exists in Application/Infrastructure layer, producing normalized Unicode NFC strings.
   - `backend/src/Infrastructure/Services/AnalyticsService.cs` (lines 25–42): Illustrates the project standard pattern for resolving department access using `GetAllowedDepartmentIdsAsync(ct)` via `ICurrentUser` and `IDepartmentAccess`.
   - `backend/src/Domain/Entities/JobApplication.cs` (lines 35–37): Stores `ResumeExtractedText` and `ResumeFileName`.
   - `backend/src/Domain/Entities/Candidate.cs` (lines 17–25): Stores `FullName`, `Email`, `Phone`.
   - `backend/src/Domain/Entities/JobPosting.cs` (lines 26–27): Stores `Title` and `Description`.
   - `backend/src/Domain/Entities/Requisition.cs` (lines 18–19): Stores `Title` and `JobDescription`.
   - `frontend/internal/src/components/Header.tsx` (lines 24–47) & `AppLayout.tsx` (lines 20–30): Global `Ctrl+K` / `Cmd+K` keyboard shortcut listener and command palette button are already present.

3. **Database & Testing Setup**:
   - `backend/src/Infrastructure/RecruitOps.Infrastructure.csproj` (line 11): `Npgsql.EntityFrameworkCore.PostgreSQL` (v10.0.0) is referenced.
   - `backend/tests/RecruitOps.Api.Tests/CustomWebAppFactory.cs` (line 92): Integration tests use `UseInMemoryDatabase`. Database search logic must remain compatible with EF Core InMemory provider during unit/integration testing.

---

## 2. Logic Chain

1. **Access Control Scoping**:
   - From ADR-0003, ADR-0018, and `AnalyticsService.cs`, any search query returning Candidates, Job Postings, or Requisitions must evaluate `ICurrentUser`.
   - If `_user.IsExcludedFromCandidateData` is `true` (i.e. `Approver`), candidate matches MUST be excluded (`CategoryCounts.candidates = 0`, items contains no candidates).
   - If `_user.IsDepartmentScoped` is `true` (i.e. `HiringManager`), requisitions and job postings must be filtered by `allowedDeptIds.Contains(DepartmentId)`. Candidate search must filter candidates to those with applications in `allowedDeptIds`.
   - Unscoped roles (`Admin`, `HrDirector`, `Recruiter`) search across all entities.

2. **Query Input Processing**:
   - From ADR-0009, search queries `q` typed in Zawgyi must be normalized via `_normalizer.Normalize(q).NormalizedText` to Unicode NFC before querying EF Core / database.
   - Empty or whitespace query strings return immediate empty responses (`totalCount = 0`).

3. **Database Search & Migration**:
   - `pg_trgm` extension allows fast trigram matching and substring indexing in PostgreSQL.
   - Adding `builder.HasPostgresExtension("pg_trgm");` in `AppDbContext.cs` and an EF Core Migration (`AddPgTrgmAndSearchIndexes`) adds GIN trigram indexes on text fields (`Candidates.FullName`, `JobApplications.ResumeExtractedText`, `JobPostings.Title`, `Requisitions.Title`, etc.).
   - To keep tests passing against `UseInMemoryDatabase`, the EF Core queries must use standard LINQ / `EF.Functions.Like` or `.Contains()` with C# in-memory scoring fallback when trigram SQL functions are unavailable.

4. **API Endpoint & Snippet Generation**:
   - Endpoint: `GET /api/search?q={query}&category={category}&page={page}&pageSize={pageSize}`.
   - Response includes query metadata, category counts breakdown, total count, and ranked `SearchResultItemDto` items.
   - For long fields (`ResumeExtractedText`, `Description`, `JobDescription`), a context snippet of ~150-200 characters centered on match term is extracted and highlighted with `<mark>` tags.

---

## 3. Caveats

- **No New Code Implemented**: Explorer is read-only; no C# or TypeScript code was created or modified in source directories.
- **Burmese Word Boundary Limitation**: PostgreSQL standard full-text search (`tsvector`) does not support Burmese word segmentation without spaces. Trigram matching (`pg_trgm`) over normalized Unicode is required per ADR-0009.
- **InMemory DB Limitation**: EF Core `InMemoryDatabaseProvider` used in test fixtures does not execute PostgreSQL native `pg_trgm` GIN index operators (`%` or `similarity()`). Search logic must fallback gracefully to C# string comparison / `EF.Functions.Like` during test execution.

---

## 4. Conclusion

The specification, ADRs, database requirements, scoring algorithm, API DTO contracts, and snippet generation rules for Person B - Flow 1 are fully surveyed and documented in `.agents/explorer_survey_3/analysis.md`. The design aligns with clean architecture, existing permission structures, and Myanmar script normalization standards.

---

## 5. Verification Method

To verify these findings:
1. Inspect `analysis.md` at `.agents/explorer_survey_3/analysis.md`.
2. Inspect `docs/decisions/ADR-0003-department-scoping.md`, `ADR-0009-myanmar-script-handling.md`, and `ADR-0018-approver-candidate-data-exclusion.md`.
3. Check `backend/src/Domain/RoleScope.cs` and `backend/src/Infrastructure/Services/AnalyticsService.cs` for role predicate and department access implementation patterns.
4. Run existing test suite to ensure baseline is clean:
   - Backend: `dotnet test backend/RecruitOps.sln`
   - Frontend: `npm run test` (in `frontend/internal`)
   - Typecheck: `npm run typecheck`
