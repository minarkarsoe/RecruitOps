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
