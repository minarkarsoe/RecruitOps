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
/// <param name="DescriptionSnippet">Extracted text snippet (~180 chars) with search terms enclosed in &lt;mark&gt; HTML tags.</param>
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
