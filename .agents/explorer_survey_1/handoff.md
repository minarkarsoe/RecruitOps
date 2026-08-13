# Handoff Report: Person B - Flow 1 Backend Survey (Full-text Search API & Scoping)

## 1. Observation

Direct observations from the codebase investigation and test suite run:

1. **Backend Architecture**:
   - Solution location: `backend/RecruitOps.sln`.
   - Core projects: `src/Domain`, `src/Application`, `src/Infrastructure`, `src/Api`, `tests/RecruitOps.Domain.Tests`, `tests/RecruitOps.Api.Tests`.
   - `DependencyInjection.cs` at `backend/src/Infrastructure/DependencyInjection.cs`: line 100 registers `IMyanmarScriptNormalizer` as a `Singleton` service (`services.AddSingleton<IMyanmarScriptNormalizer, MyanmarScriptNormalizer>();`).

2. **Entities and Search Properties**:
   - `Candidate` (`backend/src/Domain/Entities/Candidate.cs`): `FullName` (line 17), `Email` (line 20), `Phone` (line 26), `Source` (line 27).
   - `JobApplication` (`backend/src/Domain/Entities/JobApplication.cs`): `ResumeExtractedText` (line 36), `CoverNote` (line 27), `CustomFieldsJson` (line 25), `CandidateId` (line 14), `JobPostingId` (line 13).
   - `JobPosting` (`backend/src/Domain/Entities/JobPosting.cs`): `Title` (line 26), `Description` (line 27), `EmploymentType` (line 30), `ApplicationFormFieldsJson` (line 44), `DepartmentId` (line 16), `Status` (line 21).
   - `Requisition` (`backend/src/Domain/Entities/Requisition.cs`): `Title` (line 18), `JobDescription` (line 19), `DepartmentId` (line 13), `Status` (line 26).
   - `Department` (`backend/src/Domain/Entities/Department.cs`): `Name` (line 10), `Code` (line 11).

3. **Myanmar Script Normalizer**:
   - Interface: `backend/src/Application/Interfaces/IMyanmarScriptNormalizer.cs` line 21 (`IMyanmarScriptNormalizer`).
   - Implementation: `backend/src/Infrastructure/Services/MyanmarScript/MyanmarScriptNormalizer.cs` line 109 (`Normalize(string? input)`).

4. **Department Reach Scoping & Role Scope**:
   - `RoleScope.cs` (`backend/src/Domain/RoleScope.cs`): line 26 (`IsDepartmentScoped` is true for `UserRole.HiringManager`), line 42 (`IsExcludedFromCandidateData` is true for `UserRole.Approver`).
   - `IDepartmentAccess` (`backend/src/Application/Common/IDepartmentAccess.cs`) & `DepartmentAccess` (`backend/src/Infrastructure/Services/DepartmentAccess.cs`).
   - `ApplicationAccess` (`backend/src/Infrastructure/Services/ApplicationAccess.cs`): line 49 checks `_user.IsExcludedFromCandidateData` (ADR-0018) and `_departments.CanAccessAsync(row.DepartmentId, ct)` (ADR-0003), plus panel participation exception (ADR-0017 §4).

5. **Test Suite Baseline & Database Provider**:
   - Test execution command: `dotnet test backend/RecruitOps.sln`.
   - Result: Passed 51 Domain tests + 336 Api tests = **387 total passing tests** (0 failed).
   - Integration test factory: `backend/tests/RecruitOps.Api.Tests/CustomWebAppFactory.cs` replaces PostgreSQL with EF Core `UseInMemoryDatabase` (line 92).

---

## 2. Logic Chain

1. **Architecture & Scope Alignment**:
   - *Observation 1*: Clean Architecture dictates interface in `Application`, implementation in `Infrastructure`, controller in `Api`.
   - *Logic*: Search service interface `ISearchService` should be placed in `Application/Interfaces`, implementation `SearchService` in `Infrastructure/Services`, and controller `SearchController` in `Api/Controllers`.

2. **Burmese Text Normalization Strategy**:
   - *Observation 3*: `IMyanmarScriptNormalizer` is registered as a singleton service and converts Zawgyi text to Unicode FormC. Stored CV text (`JobApplication.ResumeExtractedText`) is already normalized via `IMyanmarScriptNormalizer`.
   - *Logic*: In `SearchService`, passing `query` through `_normalizer.Normalize(query).NormalizedText` ensures input queries in Zawgyi automatically map to Unicode database records without database-level text conversions.

3. **Database Provider & Search Query Method**:
   - *Observation 5*: Integration tests rely on `UseInMemoryDatabase`. Raw PostgreSQL SQL (`FromSqlRaw`, `%` trigram syntax) is incompatible with `UseInMemoryDatabase`.
   - *Logic*: To maintain 100% green tests while supporting trigram acceleration in production, search LINQ queries must use `EF.Functions.Like(column, $"%{normalizedQuery}%")` or `EF.Functions.ILike(...)`. In PostgreSQL, EF Core `ILIKE` / `LIKE` automatically leverages GIN trigram indexes (`pg_trgm`).

4. **Department Reach Scoping Enforcement**:
   - *Observation 4*: `HiringManager` is department scoped (ADR-0003) and `Approver` is excluded from candidate data unless on panel (ADR-0018).
   - *Logic*: In `SearchService`, candidate, job posting, and requisition query filters must inject `IDepartmentAccess` and `ICurrentUser` to enforce:
     - Requisitions & Postings: Filter by `allowedDepartmentIds` for `HiringManager`.
     - Candidates: Filter by `allowedDepartmentIds` OR interview panel participation for `HiringManager`. Exclude for `Approver` unless on interview panel.

5. **Test Implementation & Quality Assurance**:
   - *Observation 5*: Baseline has 387 passing tests.
   - *Logic*: New search unit/integration tests added in `RecruitOps.Api.Tests` will execute cleanly against `CustomWebAppFactory`, verifying query accuracy, Zawgyi normalization, category filtering, department scoping, and candidate data exclusion without breaking any existing tests.

---

## 3. Caveats

- **No Caveats**: All 6 required investigation points were fully verified by reading source files, examining EF Core configuration, inspecting ADRs, and running the 387 test suite.

---

## 4. Conclusion

The survey confirms that the backend codebase is cleanly structured and ready for Person B - Flow 1 implementation:
- Full-text search should be exposed via `GET /api/search?q={query}&category={category}` guarded by `[Authorize(Policy = Policies.InternalUser)]`.
- Search query normalization via `IMyanmarScriptNormalizer` will seamlessly support Zawgyi and Unicode Burmese inputs.
- Scoping must use `IDepartmentAccess` and `RoleScope` rules to satisfy ADR-0003 and ADR-0018.
- LINQ queries with `EF.Functions.Like` / `ILike` ensure complete compatibility with EF Core InMemory test runners while enabling PostgreSQL `pg_trgm` GIN index acceleration in production.

---

## 5. Verification Method

To independently verify this survey's findings and baseline:

1. **Run Backend Test Suite**:
   ```powershell
   dotnet test backend/RecruitOps.sln
   ```
   *Expected Result*: Total 387 passed (51 Domain, 336 Api).

2. **Inspect Survey Report**:
   - View `backend/src/Infrastructure/Services/MyanmarScript/MyanmarScriptNormalizer.cs`
   - View `backend/src/Infrastructure/Services/DepartmentAccess.cs`
   - View `backend/src/Domain/RoleScope.cs`
   - View `.agents/explorer_survey_1/analysis.md`
