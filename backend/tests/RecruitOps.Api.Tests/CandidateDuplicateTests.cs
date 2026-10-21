using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RecruitOps.Api.Auth;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.DTOs.Search;
using RecruitOps.Domain.Entities;
using RecruitOps.Domain.Enums;
using RecruitOps.Infrastructure.Persistence;
using Xunit;

namespace RecruitOps.Api.Tests;

/// <summary>Module 2.7 — two records of one person, created separately, flagged and decided by a
/// person (<c>design/internal/talent-pool.html</c>, "the merge nobody should do blind").
///
/// <para>The second record of each pair is written straight to the database: that is the case
/// this exists for — a record the public path's auto-reuse never saw (an earlier import, a match
/// missed before normalisation). Every client is a real user of its role.</para></summary>
public class CandidateDuplicateTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;
    private readonly Module3Scenario _scenario;

    public CandidateDuplicateTests(CustomWebAppFactory factory)
    {
        _factory = factory;
        _scenario = new Module3Scenario(factory);
    }

    private HttpClient Recruiter() => _scenario.Client(Roles.Recruiter, _factory.RecruiterUserId);

    /// <summary>One applicant through the public form, and a second, older record of the same person
    /// (same email, a phone the first lacks) with an application of its own.</summary>
    private async Task<(Guid Applied, Guid Imported, Guid ImportedApplication)> PairAsync(string title)
    {
        var (postingId, applicationId) = await _scenario.ApplicationAsync(title);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var applied = await db.JobApplications.IgnoreQueryFilters()
            .Where(a => a.Id == applicationId)
            .Join(db.Candidates.IgnoreQueryFilters(), a => a.CandidateId, c => c.Id, (_, c) => c)
            .SingleAsync();

        var imported = new Candidate
        {
            TenantId = _factory.TenantA,
            FullName = applied.FullName + " (imported)",
            Email = applied.Email,
            Phone = $"09{Random.Shared.NextInt64(100_000_000, 999_999_999)}",
            Source = SourceChannel.ExcelImport,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-30),
        };
        var importedApplication = new JobApplication
        {
            TenantId = _factory.TenantA,
            JobPostingId = postingId,
            CandidateId = imported.Id,
            Status = PipelineStatus.Sourced,
            Source = SourceChannel.ExcelImport,
            AppliedAt = DateTimeOffset.UtcNow.AddDays(-30),
        };
        db.Candidates.Add(imported);
        db.JobApplications.Add(importedApplication);
        await db.SaveChangesAsync();
        return (applied.Id, imported.Id, importedApplication.Id);
    }

    /// <summary>Another record, written straight to the database like the import in <see cref="PairAsync"/>.</summary>
    private async Task<Guid> RecordAsync(string name, string? email = null, string? phone = null, int daysAgo = 60)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var record = new Candidate
        {
            TenantId = _factory.TenantA,
            FullName = name,
            Email = email,
            Phone = phone,
            Source = SourceChannel.Referral,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-daysAgo),
        };
        db.Candidates.Add(record);
        await db.SaveChangesAsync();
        return record.Id;
    }

    private async Task<Candidate> CandidateAsync(Guid id)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Candidates.IgnoreQueryFilters().AsNoTracking().SingleAsync(c => c.Id == id);
    }

    [Fact]
    public async Task Two_Records_Sharing_An_Email_Are_Flagged_Older_First()
    {
        var (applied, imported, _) = await PairAsync("Duplicate Listing");

        var pairs = await Recruiter().GetFromJsonAsync<List<DuplicatePairDto>>("/api/candidates/duplicates");
        var pair = pairs!.Single(p => p.First.Id == imported || p.Second.Id == imported);

        // The imported record is 30 days older: it is First, the default to keep.
        Assert.Equal(imported, pair.First.Id);
        Assert.Equal(applied, pair.Second.Id);
        Assert.Equal("Email", pair.MatchedOn);
        Assert.Equal(1, pair.First.ApplicationCount);
        Assert.Equal(1, pair.Second.ApplicationCount);
        Assert.Equal("ExcelImport", pair.First.Source);
    }

    [Fact]
    public async Task Merging_Moves_The_Applications_Fills_Blanks_And_Leaves_A_Pointer()
    {
        var (applied, imported, importedApplication) = await PairAsync("Duplicate Merge");
        var importedPhone = (await CandidateAsync(imported)).Phone;

        // Keep the public applicant; fold in the import, which has the phone the applicant lacks.
        var res = await Recruiter().PostAsJsonAsync("/api/candidates/merge",
            new MergeCandidatesRequest { KeepCandidateId = applied, MergeCandidateId = imported });
        res.EnsureSuccessStatusCode();
        var result = (await res.Content.ReadFromJsonAsync<MergeCandidatesResultDto>())!;
        Assert.Equal(1, result.ApplicationsMoved);

        var kept = await CandidateAsync(applied);
        var merged = await CandidateAsync(imported);
        Assert.Equal(importedPhone, kept.Phone);           // a blank filled…
        Assert.Contains("@example.test", kept.Email);     // …nothing overwritten
        Assert.Equal(applied, merged.MergedIntoCandidateId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(applied, (await db.JobApplications.IgnoreQueryFilters().SingleAsync(a => a.Id == importedApplication)).CandidateId);
        Assert.Equal(2, await db.JobApplications.IgnoreQueryFilters().CountAsync(a => a.CandidateId == applied));

        // Not reversible, so answerable.
        var entry = await db.AuditLogEntries.IgnoreQueryFilters()
            .SingleAsync(e => e.Action == AuditAction.CandidatesMerged && e.TargetId == applied);
        Assert.Equal(_factory.RecruiterUserId, entry.ActorUserId);
        // Ids, never names: the log is permanent, and not every reader may see candidate data.
        Assert.Null(entry.TargetName);
        Assert.DoesNotContain(kept.FullName, entry.Summary + entry.DetailsJson);
        Assert.DoesNotContain(merged.FullName, entry.Summary + entry.DetailsJson);
        Assert.Contains(imported.ToString(), entry.DetailsJson);

        // And the pair is gone from the list.
        var pairs = await Recruiter().GetFromJsonAsync<List<DuplicatePairDto>>("/api/candidates/duplicates");
        Assert.DoesNotContain(pairs!, p => p.First.Id == imported || p.Second.Id == imported);
    }

    [Fact]
    public async Task Only_A_Flagged_Pair_Can_Be_Merged()
    {
        var (applied, imported, _) = await PairAsync("Duplicate Not Flagged");
        var (stranger, _, _) = await PairAsync("Duplicate Stranger");

        // Shares nothing with `applied`: the screen would never offer it, and the API refuses it too.
        var strangers = await Recruiter().PostAsJsonAsync("/api/candidates/merge",
            new MergeCandidatesRequest { KeepCandidateId = applied, MergeCandidateId = stranger });
        var self = await Recruiter().PostAsJsonAsync("/api/candidates/merge",
            new MergeCandidatesRequest { KeepCandidateId = applied, MergeCandidateId = applied });
        var missing = await Recruiter().PostAsJsonAsync("/api/candidates/merge",
            new MergeCandidatesRequest { KeepCandidateId = applied, MergeCandidateId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.Conflict, strangers.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, self.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        (await Recruiter().PostAsJsonAsync("/api/candidates/merge",
            new MergeCandidatesRequest { KeepCandidateId = applied, MergeCandidateId = imported })).EnsureSuccessStatusCode();
        var again = await Recruiter().PostAsJsonAsync("/api/candidates/merge",
            new MergeCandidatesRequest { KeepCandidateId = imported, MergeCandidateId = applied });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode); // already merged
    }

    [Fact]
    public async Task Not_The_Same_Person_Is_Remembered_And_Blocks_A_Later_Merge()
    {
        var (applied, imported, _) = await PairAsync("Duplicate Distinct");

        var first = await Recruiter().PostAsJsonAsync("/api/candidates/keep-separate",
            new KeepCandidatesSeparateRequest { FirstCandidateId = applied, SecondCandidateId = imported });
        // Either order, and twice, is the same answer.
        var second = await Recruiter().PostAsJsonAsync("/api/candidates/keep-separate",
            new KeepCandidatesSeparateRequest { FirstCandidateId = imported, SecondCandidateId = applied });
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);

        // It stops a merge for good, so it is answerable — once, however often it is asked.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var (lo, hi) = applied.CompareTo(imported) < 0 ? (applied, imported) : (imported, applied);
            var entry = await db.AuditLogEntries.IgnoreQueryFilters()
                .SingleAsync(e => e.Action == AuditAction.CandidatesKeptSeparate && e.TargetId == lo);
            Assert.Equal(_factory.RecruiterUserId, entry.ActorUserId);
            Assert.Contains(hi.ToString(), entry.DetailsJson);
            Assert.Null(entry.TargetName);
        }

        var pairs = await Recruiter().GetFromJsonAsync<List<DuplicatePairDto>>("/api/candidates/duplicates");
        Assert.DoesNotContain(pairs!, p => p.First.Id == imported || p.Second.Id == imported);

        var merge = await Recruiter().PostAsJsonAsync("/api/candidates/merge",
            new MergeCandidatesRequest { KeepCandidateId = applied, MergeCandidateId = imported });
        Assert.Equal(HttpStatusCode.Conflict, merge.StatusCode);
    }

    [Fact]
    public async Task An_Earlier_Merge_Points_At_The_Final_Record_Not_Through_A_Chain()
    {
        var (applied, imported, _) = await PairAsync("Duplicate Chain");
        // A third record of the same person, merged into the import before the import is merged on.
        Guid third;
        third = await RecordAsync("Third record", phone: (await CandidateAsync(imported)).Phone);

        (await Recruiter().PostAsJsonAsync("/api/candidates/merge",
            new MergeCandidatesRequest { KeepCandidateId = imported, MergeCandidateId = third })).EnsureSuccessStatusCode();
        (await Recruiter().PostAsJsonAsync("/api/candidates/merge",
            new MergeCandidatesRequest { KeepCandidateId = applied, MergeCandidateId = imported })).EnsureSuccessStatusCode();

        Assert.Equal(applied, (await CandidateAsync(third)).MergedIntoCandidateId);
    }

    /// <summary>What <c>CandidateDuplicateService.MergeAsync</c> writes, done by a reviewer who read
    /// the two records before someone else's decision committed.</summary>
    private static void StaleMerge(Candidate keep, Candidate other)
    {
        other.MergedIntoCandidateId = keep.Id;
        keep.MergeStamp = Guid.NewGuid();
        other.MergeStamp = Guid.NewGuid();
    }

    [Fact]
    public async Task Two_Opposite_Merges_Cannot_Both_Commit()
    {
        // A merges into B and B into A, at the same moment: each sets a different row's pointer, so
        // without a guard both would commit and leave a loop. Every merge renews both records' stamp,
        // a concurrency token — so the one that commits second fails.
        var (applied, imported, _) = await PairAsync("Duplicate Race");

        using var slow = _factory.Services.CreateScope();
        var slowDb = slow.ServiceProvider.GetRequiredService<AppDbContext>();
        var a = await slowDb.Candidates.IgnoreQueryFilters().SingleAsync(c => c.Id == applied);
        var b = await slowDb.Candidates.IgnoreQueryFilters().SingleAsync(c => c.Id == imported);

        // The other reviewer's merge commits first: imported into applied.
        (await Recruiter().PostAsJsonAsync("/api/candidates/merge",
            new MergeCandidatesRequest { KeepCandidateId = applied, MergeCandidateId = imported })).EnsureSuccessStatusCode();

        // The slow one, read before that, tries the opposite.
        StaleMerge(keep: b, other: a);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => slowDb.SaveChangesAsync());
    }

    [Fact]
    public async Task A_Record_Cannot_Be_Merged_Away_While_Another_Is_Merged_Into_It()
    {
        // Three records of one person. One reviewer folds A into B; another, at the same moment, folds C
        // into A. The second changes nothing on A but its stamp — and without the stamp the first would
        // still commit, leaving C pointing at A pointing at B, with C's applications stranded on A.
        var (applied, imported, _) = await PairAsync("Duplicate Three Way");
        var third = await RecordAsync("Third Way record", email: (await CandidateAsync(applied)).Email, daysAgo: 90);

        using var slow = _factory.Services.CreateScope();
        var slowDb = slow.ServiceProvider.GetRequiredService<AppDbContext>();
        var a = await slowDb.Candidates.IgnoreQueryFilters().SingleAsync(c => c.Id == applied);
        var b = await slowDb.Candidates.IgnoreQueryFilters().SingleAsync(c => c.Id == imported);

        // C into A commits first.
        (await Recruiter().PostAsJsonAsync("/api/candidates/merge",
            new MergeCandidatesRequest { KeepCandidateId = applied, MergeCandidateId = third })).EnsureSuccessStatusCode();

        // A into B, read before that, must not commit.
        StaleMerge(keep: b, other: a);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => slowDb.SaveChangesAsync());
        Assert.Null((await CandidateAsync(applied)).MergedIntoCandidateId);
    }

    [Fact]
    public async Task A_Merge_Cannot_Overrule_Not_The_Same_Person_Given_Meanwhile()
    {
        var (applied, imported, _) = await PairAsync("Duplicate Overrule");

        using var slow = _factory.Services.CreateScope();
        var slowDb = slow.ServiceProvider.GetRequiredService<AppDbContext>();
        var a = await slowDb.Candidates.IgnoreQueryFilters().SingleAsync(c => c.Id == applied);
        var b = await slowDb.Candidates.IgnoreQueryFilters().SingleAsync(c => c.Id == imported);

        // Another reviewer answers "not the same person" first…
        Assert.Equal(HttpStatusCode.NoContent, (await Recruiter().PostAsJsonAsync("/api/candidates/keep-separate",
            new KeepCandidatesSeparateRequest { FirstCandidateId = applied, SecondCandidateId = imported })).StatusCode);

        // …so a merge that checked before that answer existed must not commit over it.
        StaleMerge(keep: a, other: b);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => slowDb.SaveChangesAsync());
    }

    [Fact]
    public async Task Not_The_Same_Person_Follows_A_Merge()
    {
        // A and B are one person; C shares their email but is someone else, and was said to be — about B.
        var (applied, imported, _) = await PairAsync("Duplicate Carried");
        var stranger = await RecordAsync("Carried stranger", email: (await CandidateAsync(applied)).Email, daysAgo: 90);

        async Task<bool> Flagged(Guid x, Guid y) =>
            (await Recruiter().GetFromJsonAsync<List<DuplicatePairDto>>("/api/candidates/duplicates"))!
            .Any(p => (p.First.Id == x && p.Second.Id == y) || (p.First.Id == y && p.Second.Id == x));

        Assert.Equal(HttpStatusCode.NoContent, (await Recruiter().PostAsJsonAsync("/api/candidates/keep-separate",
            new KeepCandidatesSeparateRequest { FirstCandidateId = imported, SecondCandidateId = stranger })).StatusCode);
        Assert.True(await Flagged(applied, stranger)); // positive control: still asked about A

        (await Recruiter().PostAsJsonAsync("/api/candidates/merge",
            new MergeCandidatesRequest { KeepCandidateId = applied, MergeCandidateId = imported })).EnsureSuccessStatusCode();

        // B is not C and B is A, so A is not C: not asked again, and not mergeable either.
        Assert.False(await Flagged(applied, stranger));
        Assert.Equal(HttpStatusCode.Conflict, (await Recruiter().PostAsJsonAsync("/api/candidates/merge",
            new MergeCandidatesRequest { KeepCandidateId = applied, MergeCandidateId = stranger })).StatusCode);
    }

    [Fact]
    public async Task A_Value_On_Too_Many_Records_Is_A_Shared_Line_Not_A_Duplicate()
    {
        // An office number on eleven records is 55 pairs of strangers, not one person filed eleven times.
        var phone = $"09{Random.Shared.NextInt64(100_000_000, 999_999_999)}";
        var ids = new List<Guid>();
        for (var i = 0; i < 11; i++) ids.Add(await RecordAsync($"Office line {i}", phone: phone, daysAgo: 100 + i));

        var pairs = await Recruiter().GetFromJsonAsync<List<DuplicatePairDto>>("/api/candidates/duplicates");
        Assert.DoesNotContain(pairs!, p => ids.Contains(p.First.Id) || ids.Contains(p.Second.Id));
    }

    [Fact]
    public async Task Only_Recruitment_Staff_Reach_It()
    {
        var (applied, imported, _) = await PairAsync("Duplicate Authority");
        var hm = _scenario.SalesManager(); // a real Hiring Manager

        Assert.Equal(HttpStatusCode.OK, (await hm.GetAsync("/api/requisitions")).StatusCode); // the identity resolves
        Assert.Equal(HttpStatusCode.Forbidden, (await hm.GetAsync("/api/candidates/duplicates")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await hm.PostAsJsonAsync("/api/candidates/merge",
            new MergeCandidatesRequest { KeepCandidateId = applied, MergeCandidateId = imported })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await hm.PostAsJsonAsync("/api/candidates/keep-separate",
            new KeepCandidatesSeparateRequest { FirstCandidateId = applied, SecondCandidateId = imported })).StatusCode);
    }

    [Fact]
    public async Task Search_No_Longer_Finds_The_Merged_Record()
    {
        var (applied, imported, _) = await PairAsync("Duplicate Search");
        var keptName = (await CandidateAsync(applied)).FullName;
        var mergedName = (await CandidateAsync(imported)).FullName;
        async Task<IReadOnlyList<SearchResultItemDto>> Find() => (await Recruiter().GetFromJsonAsync<SearchResponseDto>(
            $"/api/search?q={Uri.EscapeDataString("Duplicate Search")}&category=Candidates"))!.Items;

        // Positive control: before the merge, search finds both.
        Assert.Contains(await Find(), i => i.Title == mergedName);

        (await Recruiter().PostAsJsonAsync("/api/candidates/merge",
            new MergeCandidatesRequest { KeepCandidateId = applied, MergeCandidateId = imported })).EnsureSuccessStatusCode();

        var after = await Find();
        Assert.DoesNotContain(after, i => i.Title == mergedName);
        Assert.Contains(after, i => i.Title == keptName);
    }
}
