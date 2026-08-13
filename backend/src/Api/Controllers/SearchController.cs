using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RecruitOps.Api.Auth;
using RecruitOps.Application.DTOs.Search;
using RecruitOps.Application.Interfaces;

namespace RecruitOps.Api.Controllers;

/// <summary>
/// Provides global full-text search across Candidates, Job Postings, and Requisitions.
/// Accessible by all internal users (Admin, HR Director, Recruiter, Hiring Manager, Approver).
/// Department Reach Scoping (ADR-0003) and Candidate Data Exclusion (ADR-0018) are enforced by <see cref="ISearchService"/>.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = Policies.InternalUser)]
public class SearchController : ControllerBase
{
    private readonly ISearchService _searchService;

    public SearchController(ISearchService searchService)
    {
        _searchService = searchService ?? throw new ArgumentNullException(nameof(searchService));
    }

    /// <summary>
    /// Executes a global full-text search with Zawgyi-to-Unicode query normalization, category filtering, and pagination.
    /// </summary>
    /// <param name="q">Search query string (Burmese Zawgyi/Unicode or English).</param>
    /// <param name="category">Category filter: "All", "Candidates", "Postings", "Requisitions" (default: "All").</param>
    /// <param name="page">1-based page index (default: 1).</param>
    /// <param name="pageSize">Items per page (default: 20, max: 100).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Categorized and ranked search results with snippets.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(SearchResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SearchResponseDto>> Search(
        [FromQuery] string? q,
        [FromQuery] string? category = "All",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        // 1. Validation: Search query is required and non-empty
        if (string.IsNullOrWhiteSpace(q))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid Search Request",
                Detail = "Search query parameter 'q' is required and cannot be empty or whitespace.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        // 2. Validation: Page must be >= 1
        if (page < 1)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid Pagination Parameter",
                Detail = "Page number must be greater than or equal to 1.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        // 3. Validation: PageSize must be between 1 and 100
        if (pageSize < 1 || pageSize > 100)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid Pagination Parameter",
                Detail = "Page size must be between 1 and 100.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var requestParams = new SearchQueryParameters(
            Q: q.Trim(),
            Category: category ?? "All",
            Page: page,
            PageSize: pageSize
        );

        var response = await _searchService.SearchAsync(requestParams, ct);
        return Ok(response);
    }
}
