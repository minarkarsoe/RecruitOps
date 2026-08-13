# Survey Analysis: Person B - Flow 1 (Full-Text Search & Command Palette)

**Agent**: explorer_survey_3  
**Date**: 2026-08-11  
**Scope**: Full-text search backend specifications, ADR compliance, PostgreSQL `pg_trgm` requirements, EF Core migrations, scoring/ranking algorithms, API schemas, and match highlighting snippet generation for Person B - Flow 1.

---

## Executive Summary

Person B - Flow 1 implements full-text search across **Candidates**, **Job Postings**, and **Requisitions** with global `Ctrl+K` Command Palette integration and a dedicated `/search?q={query}` Search Results page.

The survey examined the codebase, existing domain models, permissions model, and 5 key architectural areas:
1. **ADRs & Department Scoping**: ADR-0003 (Department Access), ADR-0009 (Myanmar Zawgyi/Unicode normalization), ADR-0018 (Approver candidate data exclusion).
2. **Database & Migrations**: PostgreSQL `pg_trgm` extension requirement, GIN trigram indexes, and EF Core InMemory test compatibility.
3. **Scoring & Ranking**: Multi-tier weighted relevance scoring algorithm across entity titles, subtitles, description fields, and extracted CV text.
4. **API Contracts & Schemas**: Exact specification for `GET /api/search?q={query}&category={category}` and matching TypeScript types in `@recruitops/types`.
5. **Snippet Generation & Match Highlighting**: Text snippet extraction with context windowing (~150-200 chars) and `<mark>` term markup.

---

## 1. ADR & Product Specification Requirements

### 1.1 Department-Scoped Access Control (ADR-0003 & ADR-0018)
- **Hiring Manager Scoping**: `HiringManager` role sees only data belonging to their assigned departments in `UserDepartments`.
- **Approver Candidate Exclusion (ADR-0018)**: `Approver` role has **no standing reach into candidate data**. `IsExcludedFromCandidateData` returns `true` for `Approver`. Candidate search queries MUST return 0 candidates for Approvers. However, `Approver` CAN search Requisitions and Job Postings across all departments.
- **Unscoped Roles**: `Admin`, `HrDirector`, and `Recruiter` can search across all departments and all categories without restriction.
- **Entity Access Rules**:
  - **Requisitions**: Filtered by `Requisition.DepartmentId` for `HiringManager`.
  - **Job Postings**: Filtered by `JobPosting.DepartmentId` for `HiringManager`.
  - **Candidates**: Filtered for `HiringManager` to only candidates having a `JobApplication` for a `JobPosting` in accessible departments (or where the manager is an interview panel participant). For `Approver`, candidates are excluded entirely.

### 1.2 Myanmar Script Normalization (ADR-0009)
- All search query string inputs (`q`) MUST be normalized using `IMyanmarScriptNormalizer` before executing queries.
- Detection & Conversion: Converts Zawgyi-encoded Myanmar script to Unicode NFC in-process with zero network overhead.
- Database records (Candidate names, CV text, JDs) are stored as normalized Unicode NFC at ingest. Querying normalized Unicode against normalized Unicode guarantees exact and trigram matching accuracy.

---

## 2. Database & EF Core Migration Requirements

### 2.1 PostgreSQL `pg_trgm` Extension
- PostgreSQL `pg_trgm` extension is required for fast trigram fuzzy and substring searching over Burmese and English text.
- EF Core Model Configuration:
  ```csharp
  builder.HasPostgresExtension("pg_trgm");
  ```
- Trigram GIN Indexes to create via EF Core Migration (`AddPgTrgmAndSearchIndexes`):
  - `Candidates`: `FullName`, `Email`, `Phone`
  - `JobApplications`: `ResumeExtractedText`, `CoverNote`
  - `JobPostings`: `Title`, `Description`, `Location`
  - `Requisitions`: `Title`, `JobDescription`

### 2.2 EF Core InMemory Test Compatibility
- Production runs Npgsql against PostgreSQL, using `EF.Functions.ILike` or `EF.Functions.TrigramsSimilarity`.
- Unit/Integration tests run against EF Core `InMemoryDatabaseProvider` (`UseInMemoryDatabase`).
- **Critical Requirement**: Search service query logic must use EF Core methods that translate cleanly both in Npgsql/PostgreSQL AND EF Core InMemory (e.g., combining normalized `EF.Functions.Like(x, pattern)` / `.Contains(query)` with C# memory scoring fallback), preventing `InvalidOperationException` during backend test runs.

---

## 3. Scoring & Ranking Algorithm

### 3.1 Relevance Score Calculation
Results are ranked by a `RelevanceScore` (scaled 0.0 to 100.0) based on match quality and field weight:

| Category | Match Type | Match Field | Weight / Score |
| :--- | :--- | :--- | :--- |
| **Candidate** | Full Name Exact / Prefix | `Candidate.FullName` | 100.0 |
| **Candidate** | Email / Phone Match | `Candidate.Email`, `Candidate.Phone` | 90.0 |
| **Candidate** | Full Name Substring | `Candidate.FullName` | 80.0 |
| **Candidate** | Extracted CV Text Match | `JobApplication.ResumeExtractedText` | 70.0 |
| **Candidate** | Cover Note Match | `JobApplication.CoverNote` | 60.0 |
| **Posting** | Title Exact / Prefix | `JobPosting.Title` | 100.0 |
| **Posting** | Title Substring | `JobPosting.Title` | 85.0 |
| **Posting** | Description Match | `JobPosting.Description` | 70.0 |
| **Posting** | Location / Type Match | `JobPosting.Location`, `JobPosting.EmploymentType` | 60.0 |
| **Requisition** | Title Exact / Prefix | `Requisition.Title` | 100.0 |
| **Requisition** | Title Substring | `Requisition.Title` | 85.0 |
| **Requisition** | Job Description Match | `Requisition.JobDescription` | 70.0 |
| **Requisition** | Department Name Match | `Department.Name` | 65.0 |

### 3.2 Result Sorting Order
1. `RelevanceScore` DESC
2. `AppliedAt` / `PostedAt` / `CreatedAt` DESC (recency fallback)

---

## 4. API Endpoints & Request / Response DTO Schemas

### 4.1 Endpoint Specification
- **URL**: `GET /api/search`
- **Params**:
  - `q` (string, optional/required): Query term (e.g. `q=Software` or `q=မင်းအောင်`).
  - `category` (string, optional): `all` | `candidates` | `postings` | `requisitions` (default: `all`).
  - `page` (int, optional, default: 1).
  - `pageSize` (int, optional, default: 20, max: 100).
- **Authorization**: `[Authorize]` (authenticated users; role scoping enforced).

### 4.2 C# Backend DTOs (`RecruitOps.Application.DTOs`)
```csharp
namespace RecruitOps.Application.DTOs;

public record SearchResultItemDto(
    string Id,
    string Category, // "Candidate" | "Posting" | "Requisition"
    string Title,
    string Subtitle,
    string Snippet,
    double RelevanceScore,
    string TargetUrl,
    Dictionary<string, string>? Metadata
);

public record SearchResponseDto(
    string Query,
    string Category,
    int TotalCount,
    List<SearchResultItemDto> Items,
    Dictionary<string, int> CategoryCounts
);
```

### 4.3 TypeScript Interfaces (`@recruitops/types`)
```typescript
export type SearchCategory = 'all' | 'candidates' | 'postings' | 'requisitions';

export interface SearchResultItem {
  id: string;
  category: 'Candidate' | 'Posting' | 'Requisition' | string;
  title: string;
  subtitle: string;
  snippet: string;
  relevanceScore: number;
  targetUrl: string;
  metadata?: Record<string, string> | null;
}

export interface SearchResponse {
  query: string;
  category: SearchCategory | string;
  totalCount: number;
  items: SearchResultItem[];
  categoryCounts: {
    candidates: number;
    postings: number;
    requisitions: number;
  };
}
```

---

## 5. Match Term Highlighting & Snippet Generation

### 5.1 Context Window Extraction
- For long text fields (e.g., `ResumeExtractedText`, `Description`, `JobDescription`), extract a context snippet centered around the matching query term.
- Context window: ~60 characters before match, matched term, ~100 characters after match.
- Prefix/Suffix Truncation: Prepend `...` if match is not at the start; append `...` if text continues past window.

### 5.2 Term Highlighting
- Server-side Snippet markup: Matched terms within the snippet string are wrapped in HTML `<mark>` tags (e.g., `"... 5 years experience with <mark>React</mark> and Node.js ..."`).
- Frontend rendering: UI components can safely render highlighted markup or use client-side term highlighting components.

---

## 6. Discovered Features & Edge Cases

### Features Discovered
| # | Category | Feature | Description | Inputs | Outputs | Error Behavior | Discovered Via |
|---|----------|---------|-------------|--------|---------|----------------|----------------|
| 1 | Search | Zawgyi Query Normalization | Automatically converts Zawgyi query strings to Unicode NFC before querying DB | Zawgyi string `q` | Normalized Unicode string | No-op on plain text/Unicode | ADR-0009 |
| 2 | Search | Department-Scoped Filtering | Restricts candidate/job/requisition results to HM's departments | User Role + Dept Access | Filtered query results | Returns empty list for unauthorized dept | ADR-0003 |
| 3 | Search | Approver Candidate Exclusion | Excludes candidate results completely for Approver role | Approver User Role | Requisitions & Postings only | Candidates list is empty (0 count) | ADR-0018 |
| 4 | Search | Trigram & Substring Search | PostgreSQL `pg_trgm` fuzzy & substring text search | Query string `q` | Matching records across entities | Fallback substring match on InMemory DB | ADR-0009 / Specs |
| 5 | Search | Snippet Generation & Highlighting | Generates ~150-200 char text window around match with `<mark>` tags | Query term + Source text | Highlighted HTML snippet string | Returns empty/truncated string on null | Requirement R1/R3 |
| 6 | Command Palette | Global Keyboard Trigger | `Ctrl+K` / `Cmd+K` global hotkey modal opening | Keyboard event | Toggles Command Palette UI modal | Ignores if in textarea/input | Requirement R2 |

### Edge Cases
| # | Feature | Input | Observed Behavior |
|---|---------|-------|-------------------|
| 1 | Search Query | Empty or whitespace `q=""` | Returns `totalCount: 0`, empty items array, zero category counts |
| 2 | Search Category | Invalid `category="invalid"` | Defaults to `category="all"` without crashing |
| 3 | Approver Search | `q="Developer"`, Role = Approver | Returns matching Job Postings and Requisitions, but 0 Candidates |
| 4 | Zawgyi Input | Zawgyi encoded Burmese query | Converts to Unicode NFC, matches Unicode records in database |
| 5 | Extracted CV Match | Query matches text deep inside candidate's 50-page PDF CV | Returns Candidate item with snippet centered around match location |
