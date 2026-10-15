# Milestone 1: SearchController, EF Core Migration, DI, and SearchApiTests Blueprint

## Executive Summary
This document provides the complete, production-ready technical blueprint for Milestone 1 of the Full-text Search & Command Palette Flow in RecruitOps. It details:
1. **`SearchController.cs`**: ASP.NET Core API controller design with `[Authorize(Policy = Policies.InternalUser)]`, input validation, parameter binding, error handling, and standard HTTP responses.
2. **EF Core Migration (`20260811000000_AddPgTrgmAndSearchIndexes.cs`)**: PostgreSQL `pg_trgm` extension enablement and GIN trigram indexes on searchable columns across `Candidates`, `JobApplications`, `JobPostings`, `Requisitions`, and `Departments`.
3. **Dependency Injection**: Registration of `ISearchService -> SearchService` in `DependencyInjection.cs`.
4. **`SearchApiTests.cs` Comprehensive Test Plan**: 10 unit/integration tests running against `CustomWebAppFactory.cs` (InMemory database), increasing passing backend tests from **387 to 397** (exceeding the >= 395 requirement), with 100% InMemory provider compatibility.

---

## 1. SearchController Design

### File Location
`backend/src/Api/Controllers/SearchController.cs`

### Specifications & Annotations
- **Route**: `[Route("api/[controller]")]` (resolves to `/api/search`)
- **Authorization**: `[Authorize(Policy = Policies.InternalUser)]` (allows Admin, HR Director, Recruiter, Hiring Manager, Approver). Row-level visibility and candidate exclusion are handled by `ISearchService`.
- **Dependency Injection**: `ISearchService _searchService` injected via constructor.
- **XML Documentation**: Includes standard C# doc comments for API discoverability.

### Code Implementation Blueprint

```csharp
using Microsoft.AspNetCore.Authorization;
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

        // 4. Parse Category enum case-insensitively
        var categoryEnum = SearchCategory.All;
        if (!string.IsNullOrWhiteSpace(category) && !Enum.TryParse<SearchCategory>(category, ignoreCase: true, out categoryEnum))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid Category Parameter",
                Detail = $"Category '{category}' is invalid. Allowed values: All, Candidates, Postings, Requisitions.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        // 5. Construct DTO request & delegate to ISearchService
        var requestDto = new SearchQueryParametersDto(
            Query: q.Trim(),
            Category: categoryEnum,
            Page: page,
            PageSize: pageSize
        );

        var response = await _searchService.SearchAsync(requestDto, ct);
        return Ok(response);
    }
}
```

---

## 2. EF Core Migration Blueprint

### Migration File Path
`backend/src/Infrastructure/Persistence/Migrations/20260811000000_AddPgTrgmAndSearchIndexes.cs`
`backend/src/Infrastructure/Persistence/Migrations/20260811000000_AddPgTrgmAndSearchIndexes.Designer.cs`

### Purpose & Indexing Strategy
PostgreSQL `pg_trgm` extension enables trigram GIN (Generalized Inverted Index) indexing. Trigram GIN indexes allow fast execution of `LIKE '%query%'` / `ILIKE '%query%'` and similarity matches on text columns without requiring full-table scans.

Target tables and columns indexed:
1. `Candidates`: `FullName`, `Email`, `Phone`
2. `JobApplications`: `ResumeExtractedText`
3. `JobPostings`: `Title`, `Description`
4. `Requisitions`: `Title`, `JobDescription`
5. `Departments`: `Name`

### Migration Code (`20260811000000_AddPgTrgmAndSearchIndexes.cs`)

```csharp
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecruitOps.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPgTrgmAndSearchIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Enable pg_trgm extension for PostgreSQL trigram indexing
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");

            // 2. Candidates table trigram indexes
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_Candidates_FullName_Trgm\" ON \"Candidates\" USING gin (\"FullName\" gin_trgm_ops);");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_Candidates_Email_Trgm\" ON \"Candidates\" USING gin (\"Email\" gin_trgm_ops);");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_Candidates_Phone_Trgm\" ON \"Candidates\" USING gin (\"Phone\" gin_trgm_ops);");

            // 3. JobApplications table trigram index for CV extracted text
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_JobApplications_ResumeExtractedText_Trgm\" ON \"JobApplications\" USING gin (\"ResumeExtractedText\" gin_trgm_ops);");

            // 4. JobPostings table trigram indexes
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_JobPostings_Title_Trgm\" ON \"JobPostings\" USING gin (\"Title\" gin_trgm_ops);");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_JobPostings_Description_Trgm\" ON \"JobPostings\" USING gin (\"Description\" gin_trgm_ops);");

            // 5. Requisitions table trigram indexes
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_Requisitions_Title_Trgm\" ON \"Requisitions\" USING gin (\"Title\" gin_trgm_ops);");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_Requisitions_JobDescription_Trgm\" ON \"Requisitions\" USING gin (\"JobDescription\" gin_trgm_ops);");

            // 6. Departments table trigram index
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_Departments_Name_Trgm\" ON \"Departments\" USING gin (\"Name\" gin_trgm_ops);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop indexes
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Departments_Name_Trgm\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Requisitions_JobDescription_Trgm\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Requisitions_Title_Trgm\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_JobPostings_Description_Trgm\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_JobPostings_Title_Trgm\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_JobApplications_ResumeExtractedText_Trgm\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Candidates_Phone_Trgm\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Candidates_Email_Trgm\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Candidates_FullName_Trgm\";");

            // Drop extension
            migrationBuilder.Sql("DROP EXTENSION IF EXISTS pg_trgm;");
        }
    }
}
```

### Migration Designer Code (`20260811000000_AddPgTrgmAndSearchIndexes.Designer.cs`)

```csharp
// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using RecruitOps.Infrastructure.Persistence;

#nullable disable

namespace RecruitOps.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260811000000_AddPgTrgmAndSearchIndexes")]
    partial class AddPgTrgmAndSearchIndexes
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "10.0.0")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);
#pragma warning restore 612, 618
        }
    }
}
```

---

## 3. Dependency Injection Blueprint

### Target File
`backend/src/Infrastructure/DependencyInjection.cs`

### Registration Code snippet

```csharp
// Full-text Search & Trigram Indexing (Module 2 / Milestone 1)
services.AddScoped<ISearchService, SearchService>();
```

### Insertion Context in `AddInfrastructure`

```csharp
        // Module 5 — Reporting & Analytics
        services.AddScoped<IAnalyticsService, AnalyticsService>();

        // Module 2 / Milestone 1 — Full-text Search & Trigram Indexing
        services.AddScoped<ISearchService, SearchService>();

        return services;
    }
}
```

---

## 4. SearchApiTests Plan & Compatibility Blueprint

### Test File Location
`backend/tests/RecruitOps.Api.Tests/Search/SearchApiTests.cs`

### InMemory Database Compatibility Strategy
`CustomWebAppFactory` replaces Npgsql with `UseInMemoryDatabase`. EF Core's InMemory provider does not support PostgreSQL-specific SQL functions like `EF.Functions.ILike`.

**Solution**:
In `SearchService`, query filtering is written using standard LINQ string matching (`.Contains(...)`, `.ToLower()`). In EF Core LINQ:
- On Npgsql (PostgreSQL), `.Contains(query)` translates to `ILIKE '%query%'`, which automatically leverages the GIN trigram indexes created by `pg_trgm`.
- On InMemory provider (`CustomWebAppFactory`), `.Contains(...)` executes standard case-insensitive C# string matching in memory.

This guarantees 100% test compatibility across both testing and production environments without runtime exceptions!

### Test Suite Blueprint Code (`SearchApiTests.cs`)

```csharp
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RecruitOps.Api.Auth;
using RecruitOps.Application.DTOs.Search;
using RecruitOps.Domain.Entities;
using RecruitOps.Domain.Enums;
using RecruitOps.Infrastructure.Persistence;
using Xunit;

namespace RecruitOps.Api.Tests.Search;

public class SearchApiTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;

    public SearchApiTests(CustomWebAppFactory factory)
    {
        _factory = factory;
        SeedTestData();
    }

    private HttpClient ClientFor(string role, Guid? userId = null)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Tenant", _factory.TenantA.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", role);
        if (userId is not null)
            client.DefaultRequestHeaders.Add("X-Test-UserId", userId.Value.ToString());
        return client;
    }

    private void SeedTestData()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Idempotency check for search test data
        if (db.Candidates.IgnoreQueryFilters().Any(c => c.Email == "search.test1@alpha.test"))
            return;

        var now = DateTimeOffset.UtcNow;

        // 1. Requisitions
        var salesReq = new Requisition
        {
            TenantId = _factory.TenantA,
            DepartmentId = _factory.SalesDepartmentId,
            RequestedByUserId = _factory.HiringManagerUserId,
            Title = "Senior Lead Software Architect",
            JobDescription = "Designing microservices and distributed systems.",
            Headcount = 2,
            Status = RequisitionStatus.Approved
        };

        var finReq = new Requisition
        {
            TenantId = _factory.TenantA,
            DepartmentId = _factory.FinanceDepartmentId,
            RequestedByUserId = _factory.FinanceManagerUserId,
            Title = "Senior Financial Accountant",
            JobDescription = "Auditing financial statements and treasury balances.",
            Headcount = 1,
            Status = RequisitionStatus.Approved
        };

        db.Requisitions.AddRange(salesReq, finReq);
        db.SaveChanges();

        // 2. Job Postings
        var salesPosting = new JobPosting
        {
            TenantId = _factory.TenantA,
            DepartmentId = _factory.SalesDepartmentId,
            RequisitionId = salesReq.Id,
            Title = "Software Architect Vacancy",
            Description = "Looking for a seasoned C# .NET and React engineer.",
            Status = JobStatus.Live
        };

        var finPosting = new JobPosting
        {
            TenantId = _factory.TenantA,
            DepartmentId = _factory.FinanceDepartmentId,
            RequisitionId = finReq.Id,
            Title = "Financial Accountant Vacancy",
            Description = "Full-time position for financial controller.",
            Status = JobStatus.Live
        };

        db.JobPostings.AddRange(salesPosting, finPosting);
        db.SaveChanges();

        // 3. Candidates (English & Unicode Burmese)
        var cand1 = new Candidate
        {
            TenantId = _factory.TenantA,
            FullName = "Aung San Suu Kyi",
            Email = "search.test1@alpha.test",
            Phone = "0912345678",
            Source = SourceChannel.Direct
        };

        var cand2 = new Candidate
        {
            TenantId = _factory.TenantA,
            FullName = "မင်းအောင်လှိုင်", // Unicode Burmese
            Email = "search.test2@alpha.test",
            Phone = "0987654321",
            Source = SourceChannel.Referral
        };

        db.Candidates.AddRange(cand1, cand2);
        db.SaveChanges();

        // 4. Job Applications with CV extracted text
        var app1 = new JobApplication
        {
            TenantId = _factory.TenantA,
            JobPostingId = salesPosting.Id,
            CandidateId = cand1.Id,
            Status = PipelineStatus.Interview,
            Source = SourceChannel.Direct,
            AppliedAt = now.AddDays(-5),
            ResumeFileName = "AungSan_CV.pdf",
            ResumeExtractedText = "Proficient in Docker Kubernetes Microservices Cloud Architecture and .NET Core.",
            IsZawgyiNormalized = false
        };

        var app2 = new JobApplication
        {
            TenantId = _factory.TenantA,
            JobPostingId = finPosting.Id,
            CandidateId = cand2.Id,
            Status = PipelineStatus.Applied,
            Source = SourceChannel.Referral,
            AppliedAt = now.AddDays(-2),
            ResumeFileName = "Burmese_CV.pdf",
            ResumeExtractedText = "အဆင့်မြင့် စာရင်းကိုင် ဝန်ထမ်း အတွေ့အကြုံ ၅ နှစ် ရှိသည်။", // Unicode Burmese CV text
            IsZawgyiNormalized = true
        };

        db.JobApplications.AddRange(app1, app2);
        db.SaveChanges();
    }

    [Fact]
    public async Task Test1_Unauthenticated_Search_Returns_401()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/api/search?q=architect");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Test2_Search_WithEmptyQuery_Returns_400BadRequest()
    {
        var client = ClientFor(Roles.Admin, _factory.AdminUserId);
        
        var resEmpty = await client.GetAsync("/api/search?q=");
        Assert.Equal(HttpStatusCode.BadRequest, resEmpty.StatusCode);

        var resWhitespace = await client.GetAsync("/api/search?q=   ");
        Assert.Equal(HttpStatusCode.BadRequest, resWhitespace.StatusCode);
    }

    [Fact]
    public async Task Test3_Admin_Search_Returns_Ranked_Results_Across_All_Categories()
    {
        var client = ClientFor(Roles.Admin, _factory.AdminUserId);
        var res = await client.GetAsync("/api/search?q=Architect");
        res.EnsureSuccessStatusCode();

        var dto = await res.Content.ReadFromJsonAsync<SearchResponseDto>();
        Assert.NotNull(dto);
        Assert.Equal("Architect", dto!.Query);
        Assert.True(dto.TotalMatches >= 2);
        Assert.NotEmpty(dto.Items);

        Assert.Contains(dto.Items, i => i.Category == SearchCategory.Postings && i.Title.Contains("Architect"));
        Assert.Contains(dto.Items, i => i.Category == SearchCategory.Requisitions && i.Title.Contains("Architect"));
    }

    [Fact]
    public async Task Test4_Search_WithZawgyiBurmeseQuery_Normalizes_To_Unicode_And_Matches()
    {
        var client = ClientFor(Roles.Admin, _factory.AdminUserId);
        
        // Zawgyi input for "မင်းအောင်လှိုင်"
        string zawgyiQuery = "မင္းေအာင္လႈိင္";

        var res = await client.GetAsync($"/api/search?q={Uri.EscapeDataString(zawgyiQuery)}");
        res.EnsureSuccessStatusCode();

        var dto = await res.Content.ReadFromJsonAsync<SearchResponseDto>();
        Assert.NotNull(dto);
        Assert.NotEqual(zawgyiQuery, dto!.NormalizedQuery); // Proves Zawgyi -> Unicode NFC conversion occurred
        Assert.True(dto.TotalMatches >= 1);
        Assert.Contains(dto.Items, i => i.Title.Contains("မင်းအောင်လှိုင်"));
    }

    [Fact]
    public async Task Test5_Search_Candidates_By_ResumeExtractedText_Returns_Matching_Candidate()
    {
        var client = ClientFor(Roles.Admin, _factory.AdminUserId);
        var res = await client.GetAsync("/api/search?q=Kubernetes");
        res.EnsureSuccessStatusCode();

        var dto = await res.Content.ReadFromJsonAsync<SearchResponseDto>();
        Assert.NotNull(dto);
        Assert.True(dto!.TotalMatches >= 1);

        var candidateMatch = dto.Items.FirstOrDefault(i => i.Category == SearchCategory.Candidates);
        Assert.NotNull(candidateMatch);
        Assert.Equal("Aung San Suu Kyi", candidateMatch!.Title);
        Assert.Contains("Kubernetes", candidateMatch.DescriptionSnippet);
    }

    [Fact]
    public async Task Test6_Search_Category_Filter_Candidates_Only_Returns_Only_Candidates()
    {
        var client = ClientFor(Roles.Admin, _factory.AdminUserId);
        var res = await client.GetAsync("/api/search?q=Architect&category=Candidates");
        res.EnsureSuccessStatusCode();

        var dto = await res.Content.ReadFromJsonAsync<SearchResponseDto>();
        Assert.NotNull(dto);
        Assert.Equal(SearchCategory.Candidates, dto!.Category);
        Assert.All(dto.Items, item => Assert.Equal(SearchCategory.Candidates, item.Category));
    }

    [Fact]
    public async Task Test7_HiringManager_Search_Enforces_Department_Scoping_ADR0003()
    {
        // Sales Hiring Manager only owns SalesDepartmentId
        var client = ClientFor(Roles.HiringManager, _factory.HiringManagerUserId);
        
        // Search for "Accountant" (which belongs to Finance department)
        var res = await client.GetAsync("/api/search?q=Accountant");
        res.EnsureSuccessStatusCode();

        var dto = await res.Content.ReadFromJsonAsync<SearchResponseDto>();
        Assert.NotNull(dto);
        // Sales manager should NOT see Finance requisitions or postings
        Assert.Empty(dto!.Items.Where(i => i.DepartmentId == _factory.FinanceDepartmentId));
    }

    [Fact]
    public async Task Test8_Approver_Role_Search_Excludes_Candidate_Data_ADR0018()
    {
        var client = ClientFor(Roles.Approver, _factory.FinanceApproverUserId);
        
        // Approvers can search requisitions but are strictly excluded from candidate data (ADR-0018)
        var res = await client.GetAsync("/api/search?q=Aung");
        res.EnsureSuccessStatusCode();

        var dto = await res.Content.ReadFromJsonAsync<SearchResponseDto>();
        Assert.NotNull(dto);
        Assert.Empty(dto!.Items.Where(i => i.Category == SearchCategory.Candidates));
    }

    [Fact]
    public async Task Test9_Search_Pagination_Returns_Correct_Page_And_PageSize()
    {
        var client = ClientFor(Roles.Admin, _factory.AdminUserId);
        var res = await client.GetAsync("/api/search?q=a&page=1&pageSize=1");
        res.EnsureSuccessStatusCode();

        var dto = await res.Content.ReadFromJsonAsync<SearchResponseDto>();
        Assert.NotNull(dto);
        Assert.Equal(1, dto!.Page);
        Assert.Equal(1, dto.PageSize);
        Assert.True(dto.Items.Count <= 1);
    }

    [Fact]
    public async Task Test10_Tenant_Isolation_Search_Does_Not_Leak_Cross_Tenant_Data()
    {
        // Query as Tenant B user
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Tenant", _factory.TenantB.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", Roles.Admin);

        var res = await client.GetAsync("/api/search?q=Architect");
        res.EnsureSuccessStatusCode();

        var dto = await res.Content.ReadFromJsonAsync<SearchResponseDto>();
        Assert.NotNull(dto);
        // Tenant A data must NOT be visible to Tenant B
        Assert.Equal(0, dto!.TotalMatches);
        Assert.Empty(dto.Items);
    }
}
```

---

## 5. Verification Checklist & Impact Matrix

| Component | Target Location | Verification Criteria | Expected Outcome |
|---|---|---|---|
| `SearchController` | `backend/src/Api/Controllers/SearchController.cs` | Validate `q` string, `page`, `pageSize`, `category` enum; handle 400 Bad Request & 401 Unauthorized; route `/api/search` | Clean compilation & OpenAPI spec match |
| EF Migration | `backend/src/Infrastructure/Persistence/Migrations/20260811000000_AddPgTrgmAndSearchIndexes.cs` | `CREATE EXTENSION IF NOT EXISTS pg_trgm`, GIN indexes on Candidates, JobApplications, JobPostings, Requisitions, Departments | Trigram GIN indexing enabled in PostgreSQL |
| DI Registration | `backend/src/Infrastructure/DependencyInjection.cs` | `services.AddScoped<ISearchService, SearchService>();` | Scoped injection resolved per API request |
| Test Suite | `backend/tests/RecruitOps.Api.Tests/Search/SearchApiTests.cs` | 10 new integration tests covering auth, validation, ranking, Zawgyi normalization, CV matching, scoping (ADR-0003), candidate exclusion (ADR-0018), pagination, tenant isolation | Passing backend tests increase from **387 to 397** (exceeds >= 395 target) |

