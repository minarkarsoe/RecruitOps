# Technical Blueprint: Backend Search Service (DTOs, Interface & Implementation)

## Overview
This document provides the precise technical blueprint for the full-text search backend feature in RecruitOps. The search service supports bilingual search (Burmese Zawgyi/Unicode and English) across Candidates, Job Postings, and Requisitions, with automatic Zawgyi normalization via `IMyanmarScriptNormalizer`, Department Reach Scoping (ADR-0003), candidate data privacy controls (ADR-0018), 0.0-100.0 relevance scoring, `<mark>` highlighted snippet generation, and category-based pagination.

---

## 1. DTO Definitions: `backend/src/Application/DTOs/Search/SearchDtos.cs`

The DTOs define the search contract exchanged between `SearchController` and `ISearchService`. They are defined as immutable C# `record` types adhering to existing Application layer patterns.

```csharp
namespace RecruitOps.Application.DTOs.Search;

/// <summary>
/// Parameters for full-text search query execution.
/// </summary>
/// <param name="Q">Raw search query string (Zawgyi/Unicode Burmese or English).</param>
/// <param name="Category">Category filter: "All", "Candidates", "Postings", "Requisitions". Default is "All".</param>
/// <param name="Page">1-based page number. Default is 1.</param>
/// <param name="PageSize">Number of items per page. Default is 20.</param>
public record SearchQueryParameters(
    string Q = "",
    string Category = "All",
    int Page = 1,
    int PageSize = 20);

/// <summary>
/// Item-level search result representation.
/// </summary>
/// <param name="Id">Entity primary key GUID (Candidate.Id, JobPosting.Id, or Requisition.Id).</param>
/// <param name="Category">Entity category: "Candidates", "Postings", "Requisitions".</param>
/// <param name="Title">Primary display title (Candidate FullName, JobPosting Title, Requisition Title).</param>
/// <param name="Subtitle">Secondary context string (e.g. Candidate Email/Phone, JobPosting Location/EmploymentType, Requisition Department/Status).</param>
/// <param name="DescriptionSnippet">Extracted text snippet (~150-200 chars) with search terms enclosed in &lt;mark&gt; HTML tags.</param>
/// <param name="TargetUrl">Frontend detail page route (e.g. "/candidates/{id}", "/jobs/{id}", "/requisitions/{id}").</param>
/// <param name="DepartmentId">Owning department ID, if applicable.</param>
/// <param name="DepartmentName">Owning department name, if applicable.</param>
/// <param name="RelevanceScore">Relevance score from 0.0 to 100.0.</param>
/// <param name="CreatedAt">Entity creation timestamp.</param>
public record SearchResultItemDto(
    Guid Id,
    string Category,
    string Title,
    string Subtitle,
    string? DescriptionSnippet,
    string TargetUrl,
    Guid? DepartmentId,
    string? DepartmentName,
    double RelevanceScore,
    DateTimeOffset CreatedAt);

/// <summary>
/// Total match counts broken down per category.
/// </summary>
/// <param name="All">Total matching records across all categories.</param>
/// <param name="Candidates">Total matching candidate records.</param>
/// <param name="Postings">Total matching job posting records.</param>
/// <param name="Requisitions">Total matching requisition records.</param>
public record CategoryCountsDto(
    int All,
    int Candidates,
    int Postings,
    int Requisitions);

/// <summary>
/// Complete response contract returned by the search service.
/// </summary>
/// <param name="Query">Original input query string.</param>
/// <param name="NormalizedQuery">Zawgyi-to-Unicode normalized query string.</param>
/// <param name="Category">Active category filter applied ("All", "Candidates", "Postings", "Requisitions").</param>
/// <param name="TotalMatches">Total match count for the active category filter.</param>
/// <param name="CategoryCounts">Category breakdown count summary.</param>
/// <param name="Items">Paginated items sorted by relevance score descending.</param>
/// <param name="Page">Current page number.</param>
/// <param name="PageSize">Requested page size.</param>
/// <param name="TotalPages">Total calculated pages for the active category filter.</param>
public record SearchResponseDto(
    string Query,
    string NormalizedQuery,
    string Category,
    int TotalMatches,
    CategoryCountsDto CategoryCounts,
    IReadOnlyList<SearchResultItemDto> Items,
    int Page,
    int PageSize,
    int TotalPages);
```

---

## 2. Interface Definition: `backend/src/Application/Interfaces/ISearchService.cs`

The interface defines the primary contract for full-text search.

```csharp
namespace RecruitOps.Application.Interfaces;

using RecruitOps.Application.DTOs.Search;

/// <summary>
/// Provides unified full-text search capabilities across Candidates, Job Postings, and Requisitions.
/// </summary>
public interface ISearchService
{
    /// <summary>
    /// Executes full-text search with Zawgyi input normalization, department reach scoping (ADR-0003),
    /// candidate privacy enforcement (ADR-0018), relevance scoring, snippet extraction, and pagination.
    /// </summary>
    /// <param name="queryParams">Query parameters including search text 'Q', 'Category', 'Page', and 'PageSize'.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="SearchResponseDto"/> containing ranked search items and category counts.</returns>
    Task<SearchResponseDto> SearchAsync(SearchQueryParameters queryParams, CancellationToken ct = default);
}
```

---

## 3. SearchService Implementation Blueprint: `backend/src/Infrastructure/Services/SearchService.cs`

### 3.1 Class Skeleton & Injected Dependencies

```csharp
namespace RecruitOps.Infrastructure.Services;

using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using RecruitOps.Application.Common;
using RecruitOps.Application.DTOs.Search;
using RecruitOps.Application.Interfaces;
using RecruitOps.Infrastructure.Persistence;

public class SearchService : ISearchService
{
    private readonly AppDbContext _db;
    private readonly IMyanmarScriptNormalizer _scriptNormalizer;
    private readonly ICurrentUser _user;
    private readonly IDepartmentAccess _access;

    public SearchService(
        AppDbContext db,
        IMyanmarScriptNormalizer scriptNormalizer,
        ICurrentUser user,
        IDepartmentAccess access)
    {
        _db = db;
        _scriptNormalizer = scriptNormalizer;
        _user = user;
        _access = access;
    }
    
    // Core implementation detailed below...
}
```

### 3.2 Query Input Normalization
1. Check if `queryParams.Q` is null, empty, or whitespace:
   - If empty, return a `SearchResponseDto` with empty item list, 0 total matches, category counts all set to 0, and `TotalPages = 0`.
2. Invoke `_scriptNormalizer.Normalize(queryParams.Q)`:
   - `var normResult = _scriptNormalizer.Normalize(queryParams.Q);`
   - `var rawQuery = queryParams.Q.Trim();`
   - `var normalizedQuery = normResult.NormalizedText.Trim();`
3. Lowercase search term for case-insensitive matching:
   - `var searchTermLower = normalizedQuery.ToLowerInvariant();`

### 3.3 Security & Department Reach Scoping (ADR-0003 & ADR-0018)
1. **Candidate Data Exclusion (ADR-0018)**:
   - Check `_user.IsExcludedFromCandidateData`. If `true` (e.g., Approver role), candidate search execution is completely bypassed and candidate match count is forced to `0`.
2. **Department Reach Scoping (ADR-0003)**:
   - Check `_user.IsDepartmentScoped`.
   - If `true` (e.g., Hiring Manager role):
     - Retrieve allowed departments: `var allowedDeptIds = (await _access.AccessibleDepartmentIdsAsync(ct)).ToHashSet();`
     - If `allowedDeptIds` is empty, no scoped entities (JobPostings, Requisitions, Candidates) can be accessed.
     - **Requisition Scoping**: `r => allowedDeptIds.Contains(r.DepartmentId)`
     - **JobPosting Scoping**: `p => allowedDeptIds.Contains(p.DepartmentId)`
     - **Candidate Scoping**: Candidate must have at least one application to a posting within `allowedDeptIds`.
   - If `false` (unscoped roles such as Admin, HrDirector, Recruiter):
     - No department filter applied.

### 3.4 Text Matching on Entities

#### 1. Candidate Entity Matching
- Join `Candidate` with `JobApplication` (and `JobPosting` / `Department` for department resolution and scoping).
- **Match Fields**:
  - `Candidate.FullName`
  - `Candidate.Email`
  - `Candidate.Phone`
  - `JobApplication.ResumeExtractedText` (Text extracted from PDF/DOCX CVs, pre-normalized to Unicode NFC upon upload)
  - `JobApplication.CoverNote`
  - `JobApplication.CustomFieldsJson`
- **EF Core Matching Expression**:
  `EF.Functions.ILike(c.FullName, $"%{normalizedQuery}%") || EF.Functions.ILike(c.Email, $"%{normalizedQuery}%") || ...`
  (Using `ILike` ensures PostgreSQL `pg_trgm` index utilization in production while remaining compatible with EF Core In-Memory database testing).

#### 2. JobPosting Entity Matching
- Table `JobPosting`, joined with `Department`.
- **Match Fields**:
  - `Title`
  - `Description`
  - `Location`
  - `ApplicationFormFieldsJson`

#### 3. Requisition Entity Matching
- Table `Requisition`, joined with `Department`.
- **Match Fields**:
  - `Title`
  - `JobDescription`

### 3.5 Relevance Score Calculation (0.0 to 100.0)

A deterministic scoring engine evaluates match quality and field weight:

| Priority / Weight Tier | Match Condition | Base Score |
|---|---|---|
| **Tier 1 (Exact Match)** | Title or FullName exact match (`String.Equals`) | **100.0** |
| **Tier 2 (Prefix Match)** | Title or FullName starts with query term | **95.0** |
| **Tier 3 (Contact Exact)** | Candidate Email or Phone exact match | **90.0** |
| **Tier 4 (Title Substring)** | Title or FullName contains query term | **85.0** |
| **Tier 5 (Contact Substring)** | Candidate Email or Phone contains query term | **80.0** |
| **Tier 6 (Location Match)** | JobPosting Location contains query term | **75.0** |
| **Tier 7 (Cover Note)** | Application CoverNote contains query term | **70.0** |
| **Tier 8 (CV Text Match)** | Candidate ResumeExtractedText contains query term | **65.0** |
| **Tier 9 (Job Description)** | JobPosting Description or Requisition JobDescription contains query term | **60.0** |
| **Tier 10 (JSON Fields)** | CustomFieldsJson or ApplicationFormFieldsJson contains query term | **45.0** |

**Frequency Bonus**:
Add `+2.0` points for each additional occurrence of `normalizedQuery` in content text, capped at `100.0`.

### 3.6 Context Snippet Extraction with `<mark>` Highlighting

```csharp
private string? ExtractHighlightedSnippet(string? content, string searchTerm, int maxChars = 180)
{
    if (string.IsNullOrWhiteSpace(content) || string.IsNullOrWhiteSpace(searchTerm))
        return null;

    int matchIndex = content.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase);
    if (matchIndex < 0)
    {
        // Fallback: return lead text truncated
        string truncate = content.Length <= maxChars ? content : content.Substring(0, maxChars) + "...";
        return System.Net.WebUtility.HtmlEncode(truncate);
    }

    // Centered window calculation around matchIndex
    int half = maxChars / 2;
    int startIndex = Math.Max(0, matchIndex - half);
    int length = Math.Min(content.Length - startIndex, maxChars);

    string rawSlice = content.Substring(startIndex, length);
    if (startIndex > 0) rawSlice = "..." + rawSlice;
    if (startIndex + length < content.Length) rawSlice += "...";

    // Encode HTML special chars first to prevent XSS
    string encodedSlice = System.Net.WebUtility.HtmlEncode(rawSlice);
    string encodedSearchTerm = System.Net.WebUtility.HtmlEncode(searchTerm);

    // Apply <mark> highlighting using Regex case-insensitive replacement
    string highlighted = Regex.Replace(
        encodedSlice,
        Regex.Escape(encodedSearchTerm),
        "<mark>$0</mark>",
        RegexOptions.IgnoreCase);

    return highlighted;
}
```

### 3.7 Category Filtering, Aggregation & Pagination Logic

1. Execute search against all eligible entity sets to derive individual category counts:
   - `candidatesCount`
   - `postingsCount`
   - `requisitionsCount`
   - `allCount = candidatesCount + postingsCount + requisitionsCount`
   - Build `CategoryCountsDto(allCount, candidatesCount, postingsCount, requisitionsCount)`.

2. Filter items according to requested `queryParams.Category`:
   - If `"Candidates"`: Return candidate items.
   - If `"Postings"`: Return job posting items.
   - If `"Requisitions"`: Return requisition items.
   - If `"All"` (default): Combine items from all three entity sets into a single unified list.

3. Global Sorting & Ranking:
   Order candidate/posting/requisition items by `RelevanceScore` descending, followed by `CreatedAt` descending as tie-breaker.

4. Apply Pagination:
   - `int totalMatches = filteredItems.Count;`
   - `int page = Math.Max(1, queryParams.Page);`
   - `int pageSize = Math.Clamp(queryParams.PageSize, 1, 100);`
   - `int totalPages = (int)Math.Ceiling((double)totalMatches / pageSize);`
   - `var pagedItems = filteredItems.Skip((page - 1) * pageSize).Take(pageSize).ToList();`

5. Return `SearchResponseDto`.

---

## 4. Dependency Injection Registration

In `backend/src/Infrastructure/DependencyInjection.cs`:

```csharp
// Module 6 — Full-text Search Service
services.AddScoped<ISearchService, SearchService>();
```

---

## Summary of Completed Technical Specifications
- **SearchDtos.cs**: 4 records (`SearchQueryParameters`, `SearchResultItemDto`, `CategoryCountsDto`, `SearchResponseDto`).
- **ISearchService.cs**: Interface with `SearchAsync` method.
- **SearchService.cs**: Complete logic addressing Zawgyi normalization, entity matching, relevance scoring, HTML highlighting, department reach scoping, and category pagination.
