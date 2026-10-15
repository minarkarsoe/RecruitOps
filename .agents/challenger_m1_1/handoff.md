# Empirical Challenge & Handoff Report — Milestone 1 Backend Search Implementation

**Verdict**: **APPROVE**

---

## 1. Observation

### Implementation & Test Files Inspected
- `backend/src/Application/Interfaces/ISearchService.cs`
- `backend/src/Application/DTOs/Search/SearchDtos.cs`
- `backend/src/Infrastructure/Services/SearchService.cs`
- `backend/src/Api/Controllers/SearchController.cs`
- `backend/tests/RecruitOps.Api.Tests/Search/SearchApiTests.cs` (10 tests)
- `backend/tests/RecruitOps.Api.Tests/Search/SearchImplementationChallengerTests.cs` (5 empirical challenge tests)

### Commands & Results
- **Command**: `dotnet test backend/RecruitOps.sln`
- **Output**:
  ```
  Passed! - Failed: 0, Passed: 51, Skipped: 0, Total: 51 - RecruitOps.Domain.Tests.dll
  Passed! - Failed: 0, Passed: 360, Skipped: 0, Total: 360 - RecruitOps.Api.Tests.dll
  Total: 411 tests passing across the solution.
  ```

---

## 2. Logic Chain

1. **Relevance Scoring Accuracy across Titles, Skills, and CV Text**:
   - *Observation*: In `SearchService.cs:473-560`, `CalculateCandidateScore`, `CalculatePostingScore`, and `CalculateRequisitionScore` assign scores based on match location hierarchy:
     - Exact title/name match = 100.0
     - Title/name starts with = 95.0
     - Email/phone exact match = 90.0
     - Title/name substring match = 85.0
     - Email/phone substring match = 80.0
     - Location match = 75.0
     - Application cover note match = 70.0
     - Resume extracted text (CV text) / Job description match = 60.0 - 65.0
     - Frequency occurrences bonus: `+2` points per additional occurrence (capped at 100).
   - *Observation*: In `SearchService.cs:110-113`, matching items are globally ordered by `RelevanceScore` descending, then `CreatedAt` descending.
   - *Logic*: Title and candidate name matches correctly outrank full CV body matches. Additional keyword occurrences boost relevance within their match category.
   - *Empirical Proof*: `ChallengerTest1_RelevanceScoring_ExactTitleMatch_Outranks_CvMatch` verified an exact candidate name match (score 100.0) ranks ahead of a CV text match (score 69.0). `ChallengerTest2_RelevanceScoring_OccurrenceCount_IncreasesScore` verified multi-occurrence boosting (+4 bonus for 3 occurrences).

2. **Zawgyi and Unicode Burmese Query Handling**:
   - *Observation*: In `SearchService.cs:51-54`, raw query inputs are passed to `_scriptNormalizer.Normalize(rawQuery)`.
   - *Observation*: `MyanmarScriptNormalizer.cs` detects Zawgyi-exclusive codepoint patterns and transforms Zawgyi input to Unicode NFC (`NormalizationForm.FormC`).
   - *Logic*: Ingested CV text and candidate data are stored normalized in Unicode NFC. Converting incoming search queries from Zawgyi to Unicode NFC enables seamless cross-encoding substring matching.
   - *Empirical Proof*: `ChallengerTest3_ZawgyiBurmeseQuery_Converts_And_Finds_Unicode_Candidate` verified that querying with Zawgyi `\u1031\u1021\u102B\u1004\u103A` normalizes to Unicode `အောင်` and successfully matches candidate `"အောင်အောင်"`.

3. **Context Snippet Extraction (~180 Chars) & Term Markup**:
   - *Observation*: `ExtractHighlightedSnippet` (`SearchService.cs:441-471`) extracts a slice centered around `matchIndex` with `maxChars = 180`. It adds `...` prefixes/suffixes when bounded, HTML-encodes the raw slice via `System.Net.WebUtility.HtmlEncode`, and highlights matched terms with `<mark>$0</mark>` using case-insensitive regex (`RegexOptions.IgnoreCase`).
   - *Logic*: Centered slicing guarantees ~180 char context window around search terms. HTML encoding before regex substitution prevents raw script injection while `<mark>` tag wrapping accurately highlights terms without altering original character casing.
   - *Empirical Proof*: `ChallengerTest4_SnippetExtraction_Length_And_MarkTag_Correctness` verified snippet length <= 200 chars and `<mark>Kubernetes</mark>` tag generation. `ChallengerTest5_SnippetExtraction_HtmlEncoding_Preserves_SpecialChars` verified special characters like `R&D` are safely encoded to `R&amp;D` inside `<mark>`.

4. **Department Scope & Role Access Scoping**:
   - *Observation*: In `SearchService.cs:178-229`, `SearchCandidatesAsync` enforces `IsExcludedFromCandidateData` (Approvers) and `IsDepartmentScoped` (Hiring Managers). Hiring Managers only access candidate/posting/requisition items within their authorized department list. Approvers cannot view candidates unless assigned to an active interview panel.
   - *Empirical Proof*: `SearchApiTests.Test7_HiringManager_Search_Enforces_Department_Scoping_ADR0003`, `Test8_Approver_Role_Search_Excludes_Candidate_Data_ADR0018`, and `Milestone1EmpiricalAccessControlAndBoundaryTests.ADR0018_Approver_Reaches_Candidate_Only_When_On_Interview_Panel` all passed cleanly.

---

## 3. Caveats

- Database trigram indexes (`pg_trgm`) are applied via EF Core migration `20260811000000_AddPgTrgmAndSearchIndexes.cs`. Integration test suite executes against EF Core In-Memory provider, which evaluates LINQ string expressions (`Contains`, `StartsWith`). PostgreSQL runtime execution requires live PostgreSQL container instance.

---

## 4. Conclusion

Milestone 1 Backend Search implementation fully satisfies all functional, security, relevance scoring, script normalization, and snippet extraction requirements. All 411 backend unit and integration tests pass cleanly.

**Final Verdict**: **APPROVE**

---

## 5. Verification Method

To independently verify this result, execute:
```bash
dotnet test backend/RecruitOps.sln
```
Verify output shows 411 tests passing (51 Domain + 360 Api).
