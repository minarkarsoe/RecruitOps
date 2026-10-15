namespace RecruitOps.Infrastructure.Services;

using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using RecruitOps.Application.Common;
using RecruitOps.Application.DTOs.Search;
using RecruitOps.Application.Interfaces;
using RecruitOps.Domain.Entities;
using RecruitOps.Infrastructure.Persistence;

public class SearchService : ISearchService
{
    private readonly AppDbContext _db;
    private readonly IMyanmarScriptNormalizer _scriptNormalizer;
    private readonly ICurrentUser _user;
    private readonly IDepartmentAccess _departmentAccess;

    public SearchService(
        AppDbContext db,
        IMyanmarScriptNormalizer scriptNormalizer,
        ICurrentUser user,
        IDepartmentAccess departmentAccess)
    {
        _db = db;
        _scriptNormalizer = scriptNormalizer;
        _user = user;
        _departmentAccess = departmentAccess;
    }

    public async Task<SearchResponseDto> SearchAsync(SearchQueryParameters queryParams, CancellationToken ct = default)
    {
        var rawQuery = queryParams.Q ?? string.Empty;
        var category = string.IsNullOrWhiteSpace(queryParams.Category) ? "All" : queryParams.Category.Trim();
        int page = queryParams.Page < 1 ? 1 : queryParams.Page;
        int pageSize = Math.Clamp(queryParams.PageSize, 1, 100);

        if (string.IsNullOrWhiteSpace(rawQuery))
        {
            return new SearchResponseDto(
                Query: rawQuery,
                NormalizedQuery: string.Empty,
                Category: category,
                TotalMatches: 0,
                CategoryCounts: new CategoryCountsDto(0, 0, 0, 0),
                Items: Array.Empty<SearchResultItemDto>(),
                Page: page,
                PageSize: pageSize,
                TotalPages: 0);
        }

        // 1. Normalize Zawgyi Burmese search inputs to Unicode NFC via IMyanmarScriptNormalizer
        var normResult = _scriptNormalizer.Normalize(rawQuery);
        var normalizedQuery = normResult.NormalizedText.Trim();
        var searchTerm = string.IsNullOrWhiteSpace(normalizedQuery) ? rawQuery.Trim() : normalizedQuery;

        // 2. Resolve Scope Context
        var scope = await ResolveScopeContextAsync(ct);

        if (scope.IsUnauthenticated)
        {
            return new SearchResponseDto(
                Query: rawQuery,
                NormalizedQuery: normalizedQuery,
                Category: category,
                TotalMatches: 0,
                CategoryCounts: new CategoryCountsDto(0, 0, 0, 0),
                Items: Array.Empty<SearchResultItemDto>(),
                Page: page,
                PageSize: pageSize,
                TotalPages: 0);
        }

        // 3. Fetch candidate, job posting, and requisition matches
        var candidateResults = await SearchCandidatesAsync(scope, searchTerm, ct);
        var postingResults = await SearchJobPostingsAsync(scope, searchTerm, ct);
        var requisitionResults = await SearchRequisitionsAsync(scope, searchTerm, ct);

        int candidatesCount = candidateResults.Count;
        int postingsCount = postingResults.Count;
        int requisitionsCount = requisitionResults.Count;
        int allCount = candidatesCount + postingsCount + requisitionsCount;

        var categoryCounts = new CategoryCountsDto(
            All: allCount,
            Candidates: candidatesCount,
            Postings: postingsCount,
            Requisitions: requisitionsCount);

        // 4. Filter by requested category
        List<SearchResultItemDto> filteredItems;
        if (category.Equals("Candidates", StringComparison.OrdinalIgnoreCase))
        {
            filteredItems = candidateResults;
        }
        else if (category.Equals("Postings", StringComparison.OrdinalIgnoreCase))
        {
            filteredItems = postingResults;
        }
        else if (category.Equals("Requisitions", StringComparison.OrdinalIgnoreCase))
        {
            filteredItems = requisitionResults;
        }
        else
        {
            // "All" or default
            filteredItems = candidateResults.Concat(postingResults).Concat(requisitionResults).ToList();
        }

        // 5. Global Ranking: Sort by RelevanceScore descending, then CreatedAt descending
        var sortedItems = filteredItems
            .OrderByDescending(x => x.RelevanceScore)
            .ThenByDescending(x => x.CreatedAt)
            .ToList();

        int totalMatches = sortedItems.Count;
        int totalPages = totalMatches == 0 ? 0 : (int)Math.Ceiling((double)totalMatches / pageSize);

        var pagedItems = sortedItems
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new SearchResponseDto(
            Query: rawQuery,
            NormalizedQuery: normalizedQuery,
            Category: category,
            TotalMatches: totalMatches,
            CategoryCounts: categoryCounts,
            Items: pagedItems,
            Page: page,
            PageSize: pageSize,
            TotalPages: totalPages);
    }

    private record ScopeContext(
        bool IsUnauthenticated,
        bool IsExcludedFromCandidateData,
        bool IsDepartmentScoped,
        IReadOnlyCollection<Guid> AllowedDepartmentIds,
        Guid UserId);

    private async Task<ScopeContext> ResolveScopeContextAsync(CancellationToken ct)
    {
        var userId = _user.UserId;
        if (userId is null)
        {
            return new ScopeContext(
                IsUnauthenticated: true,
                IsExcludedFromCandidateData: true,
                IsDepartmentScoped: true,
                AllowedDepartmentIds: Array.Empty<Guid>(),
                UserId: Guid.Empty);
        }

        var isExcluded = _user.IsExcludedFromCandidateData;
        var isDeptScoped = _user.IsDepartmentScoped;
        IReadOnlyCollection<Guid> allowedDeptIds = Array.Empty<Guid>();

        if (isDeptScoped)
        {
            allowedDeptIds = await _departmentAccess.AccessibleDepartmentIdsAsync(ct);
        }

        return new ScopeContext(
            IsUnauthenticated: false,
            IsExcludedFromCandidateData: isExcluded,
            IsDepartmentScoped: isDeptScoped,
            AllowedDepartmentIds: allowedDeptIds,
            UserId: userId.Value);
    }

    private record AppTextData(Guid CandidateId, string? ResumeExtractedText, string? CoverNote, string? CustomFieldsJson);

    private async Task<List<SearchResultItemDto>> SearchCandidatesAsync(ScopeContext scope, string searchTerm, CancellationToken ct)
    {
        HashSet<Guid>? allowedCandidateIds = null;

        if (scope.IsExcludedFromCandidateData)
        {
            // Approver role (ADR-0018): Strictly excluded from candidate data
            // Exception: Candidate has an application where user is an interview participant
            var participantAppIds = await _db.InterviewParticipants
                .AsNoTracking()
                .Where(ip => ip.UserId == scope.UserId)
                .Join(_db.Interviews.AsNoTracking(), ip => ip.InterviewId, i => i.Id, (ip, i) => i.JobApplicationId)
                .Distinct()
                .ToListAsync(ct);

            if (!participantAppIds.Any())
            {
                return new List<SearchResultItemDto>();
            }

            var candIds = await _db.JobApplications
                .AsNoTracking()
                .Where(a => participantAppIds.Contains(a.Id))
                .Select(a => a.CandidateId)
                .Distinct()
                .ToListAsync(ct);

            allowedCandidateIds = candIds.ToHashSet();
        }
        else if (scope.IsDepartmentScoped)
        {
            // HiringManager role (ADR-0003 & ADR-0017 §4):
            // Candidate reached if has application in allowed departments OR user is an interview participant
            var participantAppIds = await _db.InterviewParticipants
                .AsNoTracking()
                .Where(ip => ip.UserId == scope.UserId)
                .Join(_db.Interviews.AsNoTracking(), ip => ip.InterviewId, i => i.Id, (ip, i) => i.JobApplicationId)
                .Distinct()
                .ToListAsync(ct);

            var deptPostingIds = await _db.JobPostings
                .AsNoTracking()
                .Where(p => scope.AllowedDepartmentIds.Contains(p.DepartmentId))
                .Select(p => p.Id)
                .ToListAsync(ct);

            var candIds = await _db.JobApplications
                .AsNoTracking()
                .Where(a => deptPostingIds.Contains(a.JobPostingId) || participantAppIds.Contains(a.Id))
                .Select(a => a.CandidateId)
                .Distinct()
                .ToListAsync(ct);

            allowedCandidateIds = candIds.ToHashSet();
        }

        IQueryable<Candidate> candQuery = _db.Candidates.AsNoTracking();
        if (allowedCandidateIds != null)
        {
            candQuery = candQuery.Where(c => allowedCandidateIds.Contains(c.Id));
        }

        var candidates = await candQuery.ToListAsync(ct);
        if (!candidates.Any())
            return new List<SearchResultItemDto>();

        var candIdsList = candidates.Select(c => c.Id).ToList();

        var candidateApps = await _db.JobApplications
            .AsNoTracking()
            .Where(a => candIdsList.Contains(a.CandidateId))
            .Select(a => new AppTextData(a.CandidateId, a.ResumeExtractedText, a.CoverNote, a.CustomFieldsJson))
            .ToListAsync(ct);

        var appsByCandidate = candidateApps
            .GroupBy(a => a.CandidateId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<SearchResultItemDto>();

        foreach (var cand in candidates)
        {
            appsByCandidate.TryGetValue(cand.Id, out var apps);
            apps ??= new List<AppTextData>();

            bool isMatch = false;
            string? matchContent = null;

            if (cand.FullName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            {
                isMatch = true;
                matchContent = cand.FullName;
            }
            else if (cand.Email != null && cand.Email.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            {
                isMatch = true;
                matchContent = cand.Email;
            }
            else if (cand.Phone != null && cand.Phone.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            {
                isMatch = true;
                matchContent = cand.Phone;
            }
            else
            {
                foreach (var app in apps)
                {
                    if (app.ResumeExtractedText != null && app.ResumeExtractedText.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                    {
                        isMatch = true;
                        matchContent = app.ResumeExtractedText;
                        break;
                    }
                    if (app.CoverNote != null && app.CoverNote.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                    {
                        isMatch = true;
                        matchContent = app.CoverNote;
                        break;
                    }
                    if (app.CustomFieldsJson != null && app.CustomFieldsJson.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                    {
                        isMatch = true;
                        matchContent = app.CustomFieldsJson;
                        break;
                    }
                }
            }

            if (isMatch)
            {
                var snippet = ExtractHighlightedSnippet(matchContent, searchTerm);
                double score = CalculateCandidateScore(cand, apps, searchTerm);
                string subtitle = string.Join(" | ", new[] { cand.Email, cand.Phone }.Where(s => !string.IsNullOrWhiteSpace(s)));

                result.Add(new SearchResultItemDto(
                    Id: cand.Id,
                    Category: "Candidates",
                    Title: cand.FullName,
                    Subtitle: subtitle,
                    DescriptionSnippet: snippet,
                    TargetUrl: $"/candidates/{cand.Id}",
                    DepartmentId: null,
                    DepartmentName: null,
                    RelevanceScore: score,
                    CreatedAt: cand.CreatedAt));
            }
        }

        return result;
    }

    private async Task<List<SearchResultItemDto>> SearchJobPostingsAsync(ScopeContext scope, string searchTerm, CancellationToken ct)
    {
        var depts = await _db.Departments.AsNoTracking().ToDictionaryAsync(d => d.Id, d => d.Name, ct);

        IQueryable<JobPosting> postingQuery = _db.JobPostings.AsNoTracking();
        if (scope.IsDepartmentScoped)
        {
            postingQuery = postingQuery.Where(p => scope.AllowedDepartmentIds.Contains(p.DepartmentId));
        }

        var postings = await postingQuery.ToListAsync(ct);
        var result = new List<SearchResultItemDto>();

        foreach (var p in postings)
        {
            bool isMatch = false;
            string? matchContent = null;

            if (p.Title.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            {
                isMatch = true;
                matchContent = p.Title;
            }
            else if (p.Description != null && p.Description.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            {
                isMatch = true;
                matchContent = p.Description;
            }
            else if (p.Location != null && p.Location.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            {
                isMatch = true;
                matchContent = p.Location;
            }
            else if (p.ApplicationFormFieldsJson != null && p.ApplicationFormFieldsJson.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            {
                isMatch = true;
                matchContent = p.ApplicationFormFieldsJson;
            }

            if (isMatch)
            {
                depts.TryGetValue(p.DepartmentId, out var deptName);
                var snippet = ExtractHighlightedSnippet(matchContent, searchTerm);
                double score = CalculatePostingScore(p, searchTerm);
                string subtitle = string.Join(" | ", new[] { p.Location, p.EmploymentType.ToString() }.Where(s => !string.IsNullOrWhiteSpace(s)));

                result.Add(new SearchResultItemDto(
                    Id: p.Id,
                    Category: "Postings",
                    Title: p.Title,
                    Subtitle: subtitle,
                    DescriptionSnippet: snippet,
                    TargetUrl: $"/jobs/{p.Id}",
                    DepartmentId: p.DepartmentId,
                    DepartmentName: deptName,
                    RelevanceScore: score,
                    CreatedAt: p.CreatedAt));
            }
        }

        return result;
    }

    private async Task<List<SearchResultItemDto>> SearchRequisitionsAsync(ScopeContext scope, string searchTerm, CancellationToken ct)
    {
        var depts = await _db.Departments.AsNoTracking().ToDictionaryAsync(d => d.Id, d => d.Name, ct);

        IQueryable<Requisition> reqQuery = _db.Requisitions.AsNoTracking();
        if (scope.IsDepartmentScoped)
        {
            reqQuery = reqQuery.Where(r => scope.AllowedDepartmentIds.Contains(r.DepartmentId));
        }

        var requisitions = await reqQuery.ToListAsync(ct);
        var result = new List<SearchResultItemDto>();

        foreach (var r in requisitions)
        {
            bool isMatch = false;
            string? matchContent = null;

            if (r.Title.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            {
                isMatch = true;
                matchContent = r.Title;
            }
            else if (r.JobDescription != null && r.JobDescription.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            {
                isMatch = true;
                matchContent = r.JobDescription;
            }

            if (isMatch)
            {
                depts.TryGetValue(r.DepartmentId, out var deptName);
                var snippet = ExtractHighlightedSnippet(matchContent, searchTerm);
                double score = CalculateRequisitionScore(r, searchTerm);
                string subtitle = string.Join(" | ", new[] { deptName, r.Status.ToString() }.Where(s => !string.IsNullOrWhiteSpace(s)));

                result.Add(new SearchResultItemDto(
                    Id: r.Id,
                    Category: "Requisitions",
                    Title: r.Title,
                    Subtitle: subtitle,
                    DescriptionSnippet: snippet,
                    TargetUrl: $"/requisitions/{r.Id}",
                    DepartmentId: r.DepartmentId,
                    DepartmentName: deptName,
                    RelevanceScore: score,
                    CreatedAt: r.CreatedAt));
            }
        }

        return result;
    }

    private static string? ExtractHighlightedSnippet(string? content, string searchTerm, int maxChars = 180)
    {
        if (string.IsNullOrWhiteSpace(content) || string.IsNullOrWhiteSpace(searchTerm))
            return null;

        int matchIndex = content.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase);
        if (matchIndex < 0)
        {
            string truncate = content.Length <= maxChars ? content : content.Substring(0, maxChars) + "...";
            return System.Net.WebUtility.HtmlEncode(truncate);
        }

        int half = maxChars / 2;
        int startIndex = Math.Max(0, matchIndex - half);
        int length = Math.Min(content.Length - startIndex, maxChars);

        string rawSlice = content.Substring(startIndex, length);
        if (startIndex > 0) rawSlice = "..." + rawSlice;
        if (startIndex + length < content.Length) rawSlice += "...";

        string encodedSlice = System.Net.WebUtility.HtmlEncode(rawSlice);
        string encodedSearchTerm = System.Net.WebUtility.HtmlEncode(searchTerm);

        string highlighted = Regex.Replace(
            encodedSlice,
            Regex.Escape(encodedSearchTerm),
            "<mark>$0</mark>",
            RegexOptions.IgnoreCase);

        return highlighted;
    }

    private static double CalculateCandidateScore(Candidate cand, List<AppTextData> apps, string searchTerm)
    {
        double baseScore;
        if (cand.FullName.Equals(searchTerm, StringComparison.OrdinalIgnoreCase))
            baseScore = 100.0;
        else if (cand.FullName.StartsWith(searchTerm, StringComparison.OrdinalIgnoreCase))
            baseScore = 95.0;
        else if ((cand.Email != null && cand.Email.Equals(searchTerm, StringComparison.OrdinalIgnoreCase)) ||
                 (cand.Phone != null && cand.Phone.Equals(searchTerm, StringComparison.OrdinalIgnoreCase)))
            baseScore = 90.0;
        else if (cand.FullName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            baseScore = 85.0;
        else if ((cand.Email != null && cand.Email.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)) ||
                 (cand.Phone != null && cand.Phone.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)))
            baseScore = 80.0;
        else if (apps.Any(a => a.CoverNote != null && a.CoverNote.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)))
            baseScore = 70.0;
        else if (apps.Any(a => a.ResumeExtractedText != null && a.ResumeExtractedText.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)))
            baseScore = 65.0;
        else
            baseScore = 45.0;

        int totalOccurrences = CountOccurrences(cand.FullName, searchTerm)
            + CountOccurrences(cand.Email, searchTerm)
            + CountOccurrences(cand.Phone, searchTerm);

        foreach (var app in apps)
        {
            totalOccurrences += CountOccurrences(app.CoverNote, searchTerm);
            totalOccurrences += CountOccurrences(app.ResumeExtractedText, searchTerm);
            totalOccurrences += CountOccurrences(app.CustomFieldsJson, searchTerm);
        }

        if (totalOccurrences > 1)
            baseScore += (totalOccurrences - 1) * 2.0;

        return Math.Min(100.0, baseScore);
    }

    private static double CalculatePostingScore(JobPosting p, string searchTerm)
    {
        double baseScore;
        if (p.Title.Equals(searchTerm, StringComparison.OrdinalIgnoreCase))
            baseScore = 100.0;
        else if (p.Title.StartsWith(searchTerm, StringComparison.OrdinalIgnoreCase))
            baseScore = 95.0;
        else if (p.Title.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            baseScore = 85.0;
        else if (p.Location != null && p.Location.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            baseScore = 75.0;
        else if (p.Description != null && p.Description.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            baseScore = 60.0;
        else
            baseScore = 45.0;

        int totalOccurrences = CountOccurrences(p.Title, searchTerm)
            + CountOccurrences(p.Description, searchTerm)
            + CountOccurrences(p.Location, searchTerm)
            + CountOccurrences(p.ApplicationFormFieldsJson, searchTerm);

        if (totalOccurrences > 1)
            baseScore += (totalOccurrences - 1) * 2.0;

        return Math.Min(100.0, baseScore);
    }

    private static double CalculateRequisitionScore(Requisition r, string searchTerm)
    {
        double baseScore;
        if (r.Title.Equals(searchTerm, StringComparison.OrdinalIgnoreCase))
            baseScore = 100.0;
        else if (r.Title.StartsWith(searchTerm, StringComparison.OrdinalIgnoreCase))
            baseScore = 95.0;
        else if (r.Title.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            baseScore = 85.0;
        else if (r.JobDescription != null && r.JobDescription.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            baseScore = 60.0;
        else
            baseScore = 50.0;

        int totalOccurrences = CountOccurrences(r.Title, searchTerm)
            + CountOccurrences(r.JobDescription, searchTerm);

        if (totalOccurrences > 1)
            baseScore += (totalOccurrences - 1) * 2.0;

        return Math.Min(100.0, baseScore);
    }

    private static int CountOccurrences(string? source, string searchTerm)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(searchTerm))
            return 0;

        int count = 0;
        int index = 0;
        while ((index = source.IndexOf(searchTerm, index, StringComparison.OrdinalIgnoreCase)) != -1)
        {
            count++;
            index += searchTerm.Length;
        }
        return count;
    }
}
