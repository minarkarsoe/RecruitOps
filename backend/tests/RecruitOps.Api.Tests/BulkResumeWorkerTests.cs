using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RecruitOps.Api.Auth;
using RecruitOps.Application.Common;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;
using RecruitOps.Domain.Entities;
using RecruitOps.Domain.Enums;
using RecruitOps.Infrastructure.Persistence;
using RecruitOps.Infrastructure.Services.Delivery;
using RecruitOps.Infrastructure.Tenancy;
using Xunit;

namespace RecruitOps.Api.Tests;

/// <summary>The bulk CV worker from ADR-0026, wired against the <b>real</b>
/// <see cref="CurrentTenant"/>.
///
/// <para>The end-to-end behaviour is covered by <c>BulkResumeUpload*Tests</c> through the API.
/// What is pinned here is what the rewrite was actually <i>for</i> — durability, the attempt cap,
/// and tenant scoping — none of which the old <c>static ConcurrentDictionary</c> could have
/// satisfied, and none of which a green API suite would have noticed the absence of.</para>
/// </summary>
public class BulkResumeWorkerTests
{
    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 8, 21, 9, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now = _now.Add(by);
    }

    /// <summary>Stands in for text extraction so the tests are about the queue, not about PDF
    /// parsing. <see cref="Behaviour"/> is what each test actually varies.</summary>
    private sealed class FakeExtractor : IDocumentTextExtractor
    {
        public Func<string, DocumentExtractionResult> Behaviour { get; set; } =
            fileName => Result(fileName, $"{fileName}@example.test", null);

        public int Calls { get; private set; }

        public Task<DocumentExtractionResult> ExtractTextAsync(
            Stream stream, string fileName, string contentType, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(Behaviour(fileName));
        }

        public static DocumentExtractionResult Result(string name, string? email, string? phone) =>
            new(
                ExtractedText: $"CV text for {name}",
                OriginalText: $"CV text for {name}",
                DetectedLanguage: "en",
                IsZawgyiNormalized: false,
                ParsedContactInfo: new ParsedContactInfoDto(name, email, phone, null, []));
    }

    private sealed class Fixture
    {
        public required ServiceProvider Provider { get; init; }
        public required InMemoryFileStorage Storage { get; init; }
        public required FakeExtractor Extractor { get; init; }
        public required TestClock Clock { get; init; }
        public required BulkResumeWorker Worker { get; init; }

        public List<BulkUploadFile> Files()
        {
            using var scope = Provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            return db.BulkUploadFiles.IgnoreQueryFilters().OrderBy(f => f.Ordinal).ToList();
        }

        public List<Candidate> Candidates()
        {
            using var scope = Provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            return db.Candidates.IgnoreQueryFilters().ToList();
        }

        public List<JobApplication> Applications()
        {
            using var scope = Provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            return db.JobApplications.IgnoreQueryFilters().ToList();
        }

        public List<ApplicationStageHistory> History()
        {
            using var scope = Provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            return db.ApplicationStageHistories.IgnoreQueryFilters().ToList();
        }
    }

    private static Fixture BuildFixture(Action<BulkResumeOptions>? configure = null)
    {
        var storage = new InMemoryFileStorage();
        var extractor = new FakeExtractor();
        var clock = new TestClock();

        var options = new BulkResumeOptions();
        configure?.Invoke(options);

        // Named once and captured — AddDbContext builds its options per scope, so a Guid inside
        // the lambda would give every scope its own database.
        var databaseName = Guid.NewGuid().ToString();

        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.None));
        // No HttpContext is ever set — exactly the worker's situation.
        services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        services.AddScoped<IAmbientTenantScope, AmbientTenantScope>();
        services.AddScoped<ICurrentTenant, CurrentTenant>();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(databaseName));
        services.AddSingleton<IFileStorage>(storage);
        services.AddSingleton<IDocumentTextExtractor>(extractor);
        services.AddSingleton<TimeProvider>(clock);
        services.AddSingleton<IOptions<BulkResumeOptions>>(Options.Create(options));
        services.AddSingleton<BulkResumeWorker>();

        var provider = services.BuildServiceProvider();

        return new Fixture
        {
            Provider = provider,
            Storage = storage,
            Extractor = extractor,
            Clock = clock,
            Worker = provider.GetRequiredService<BulkResumeWorker>(),
        };
    }

    /// <summary>Queues a batch the way the service does: bytes in storage, rows in the database.</summary>
    private static Guid Seed(
        Fixture fixture, Guid tenantId, Guid? uploaderId = null, bool store = true, params string[] fileNames)
    {
        using var scope = fixture.Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var posting = new JobPosting
        {
            TenantId = tenantId,
            DepartmentId = Guid.NewGuid(),
            RequisitionId = Guid.NewGuid(),
            Title = "Collections Officer",
            Description = "JD",
        };

        var batch = new BulkUploadBatch
        {
            TenantId = tenantId,
            JobPostingId = posting.Id,
            UploadedByUserId = uploaderId,
            CreatedAt = fixture.Clock.GetUtcNow(),
        };

        db.JobPostings.Add(posting);
        db.BulkUploadBatches.Add(batch);

        for (var i = 0; i < fileNames.Length; i++)
        {
            var row = new BulkUploadFile
            {
                TenantId = tenantId,
                BulkUploadBatchId = batch.Id,
                Ordinal = i,
                FileName = fileNames[i],
                ContentType = "application/pdf",
                SizeBytes = 1024,
                Status = BulkFileStatus.Queued,
                NextAttemptAt = fixture.Clock.GetUtcNow(),
            };
            row.StorageKey = $"bulk-uploads/{batch.Id}/{row.Id}.pdf";

            if (store)
            {
                using var bytes = new MemoryStream(new byte[] { 1, 2, 3, 4 });
                fixture.Storage.UploadAsync(
                    new UploadFileRequest(row.StorageKey, bytes, "application/pdf", 4)).GetAwaiter().GetResult();
            }

            db.BulkUploadFiles.Add(row);
        }

        db.SaveChanges();
        return batch.Id;
    }

    // ---------------------------------------------------------------- tenancy

    /// <summary>The test ADR-0026 asks for by name, in its second queue: two tenants, one pass,
    /// each file confined to its own.</summary>
    [Fact]
    public async Task Each_File_Is_Processed_In_Its_Own_Tenant()
    {
        var fixture = BuildFixture();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        Seed(fixture, tenantA, fileNames: "alpha.pdf");
        Seed(fixture, tenantB, fileNames: "bravo.pdf");

        Assert.Equal(2, await fixture.Worker.RunOnceAsync());

        var applications = fixture.Applications();
        Assert.Equal(2, applications.Count);
        Assert.Contains(applications, a => a.TenantId == tenantA);
        Assert.Contains(applications, a => a.TenantId == tenantB);

        // Every candidate created carries its own file's tenant, not the previous one's.
        foreach (var candidate in fixture.Candidates())
        {
            Assert.NotEqual(Guid.Empty, candidate.TenantId);
            Assert.Contains(candidate.TenantId, new[] { tenantA, tenantB });
        }
    }

    /// <summary>The payoff of ADR-0026 §4, and the thing the old implementation could only get
    /// right by remembering to.
    ///
    /// <para>It deduplicated candidates with <c>IgnoreQueryFilters()</c> plus a hand-written
    /// <c>c.TenantId == batchState.TenantId</c>. That worked — and it is exactly the shape ADR-0003
    /// calls out, because the day someone copies the query and drops the predicate, one company's
    /// CV silently attaches to another company's candidate. Here the filter does it, so there is
    /// no predicate to drop.</para></summary>
    [Fact]
    public async Task Deduplication_Does_Not_Reach_Across_Tenants()
    {
        var fixture = BuildFixture();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        // The same human, applying to two different companies that both run RecruitOps.
        fixture.Extractor.Behaviour = _ => FakeExtractor.Result("Aye Aye Mon", "aye@example.test", "0977000111");

        Seed(fixture, tenantA, fileNames: "a.pdf");
        Seed(fixture, tenantB, fileNames: "b.pdf");

        await fixture.Worker.RunOnceAsync();

        var candidates = fixture.Candidates();
        Assert.Equal(2, candidates.Count);
        Assert.Single(candidates, c => c.TenantId == tenantA);
        Assert.Single(candidates, c => c.TenantId == tenantB);
    }

    [Fact]
    public async Task Two_Files_For_The_Same_Person_Produce_One_Candidate()
    {
        var fixture = BuildFixture();
        var tenantId = Guid.NewGuid();

        fixture.Extractor.Behaviour = _ => FakeExtractor.Result("Min Min", "min@example.test", "0977000222");

        Seed(fixture, tenantId, fileNames: ["first.pdf", "second.pdf"]);

        await fixture.Worker.RunOnceAsync();

        Assert.Single(fixture.Candidates());
        Assert.Equal(2, fixture.Applications().Count);   // one application per CV, still
        Assert.All(fixture.Files(), f => Assert.Equal(BulkFileStatus.Success, f.Status));
    }

    // ---------------------------------------------------------------- durability

    /// <summary>What the rewrite exists for. The old implementation started work with
    /// <c>_ = Task.Run(...)</c> over a static dictionary, so a process that died mid-batch did not
    /// leave stale state — it left <b>nothing</b>, and the recruiter's fifty files answered 404.
    ///
    /// <para>Here a claim only pushes the row into the future. Nothing is marked in flight, so a
    /// worker that never comes back leaves work that becomes due again on its own.</para></summary>
    [Fact]
    public async Task A_Crash_After_Claiming_Loses_Nothing()
    {
        var fixture = BuildFixture(o => o.VisibilityTimeout = TimeSpan.FromMinutes(15));
        var tenantId = Guid.NewGuid();
        Seed(fixture, tenantId, fileNames: "resilient.pdf");

        // Stand in for a process that dies between claiming and finishing.
        fixture.Extractor.Behaviour = _ => throw new OperationCanceledException("process went away");
        await fixture.Worker.RunOnceAsync();

        var afterCrash = fixture.Files().Single();
        Assert.Equal(BulkFileStatus.Queued, afterCrash.Status);      // never "Processing"
        Assert.Equal(1, afterCrash.Attempts);
        Assert.Null(afterCrash.JobApplicationId);

        // A new process, some time later.
        fixture.Extractor.Behaviour = name => FakeExtractor.Result(name, $"{name}@example.test", null);
        fixture.Clock.Advance(TimeSpan.FromMinutes(20));

        Assert.Equal(1, await fixture.Worker.RunOnceAsync());
        Assert.Equal(BulkFileStatus.Success, fixture.Files().Single().Status);
    }

    /// <summary>The visibility timeout. A second pass immediately afterwards must not process the
    /// same CV twice — which would create the candidate twice.</summary>
    [Fact]
    public async Task Claiming_Hides_A_File_From_The_Next_Pass()
    {
        var fixture = BuildFixture();
        Seed(fixture, Guid.NewGuid(), fileNames: "once.pdf");

        Assert.Equal(1, await fixture.Worker.RunOnceAsync());
        Assert.Equal(0, await fixture.Worker.RunOnceAsync());

        Assert.Single(fixture.Applications());
        Assert.Equal(1, fixture.Extractor.Calls);
    }

    // ---------------------------------------------------------------- outcomes

    /// <summary>Bytes that are gone are gone. Retrying is a slower way of telling the recruiter to
    /// upload the file again, and it keeps a dead row circulating.</summary>
    [Fact]
    public async Task A_File_Whose_Bytes_Are_Missing_Fails_Terminally()
    {
        var fixture = BuildFixture();
        Seed(fixture, Guid.NewGuid(), store: false, fileNames: "vanished.pdf");

        await fixture.Worker.RunOnceAsync();

        var file = fixture.Files().Single();
        Assert.Equal(BulkFileStatus.Failed, file.Status);
        Assert.Equal(1, file.Attempts);                       // terminal on the first attempt
        Assert.Contains("no longer in storage", file.LastError);

        fixture.Clock.Advance(TimeSpan.FromDays(1));
        Assert.Equal(0, await fixture.Worker.RunOnceAsync()); // and never claimed again
    }

    [Fact]
    public async Task A_Transient_Failure_Is_Retried_And_Then_Succeeds()
    {
        var fixture = BuildFixture(o => o.BaseBackoff = TimeSpan.FromSeconds(30));
        Seed(fixture, Guid.NewGuid(), fileNames: "flaky.pdf");

        fixture.Extractor.Behaviour = _ => throw new IOException("storage hiccup");
        await fixture.Worker.RunOnceAsync();

        var afterFirst = fixture.Files().Single();
        Assert.Equal(BulkFileStatus.Queued, afterFirst.Status);
        Assert.Contains("storage hiccup", afterFirst.LastError);

        fixture.Extractor.Behaviour = name => FakeExtractor.Result(name, "flaky@example.test", null);
        fixture.Clock.Advance(TimeSpan.FromMinutes(2));

        await fixture.Worker.RunOnceAsync();
        Assert.Equal(BulkFileStatus.Success, fixture.Files().Single().Status);
    }

    /// <summary>A CV that can never be parsed must stop. Without the cap it would be claimed every
    /// visibility window forever, and the batch would never read as finished.</summary>
    [Fact]
    public async Task Retrying_Stops_At_The_Attempt_Cap()
    {
        var fixture = BuildFixture(o =>
        {
            o.MaxAttempts = 3;
            o.BaseBackoff = TimeSpan.FromSeconds(30);
        });
        Seed(fixture, Guid.NewGuid(), fileNames: "poison.pdf");

        fixture.Extractor.Behaviour = _ => throw new InvalidDataException("cannot parse this");

        for (var i = 0; i < 5; i++)
        {
            await fixture.Worker.RunOnceAsync();
            fixture.Clock.Advance(TimeSpan.FromHours(1));
        }

        var file = fixture.Files().Single();
        Assert.Equal(BulkFileStatus.Failed, file.Status);
        Assert.Equal(3, file.Attempts);
        Assert.Contains("Gave up after 3 attempts", file.LastError);
    }

    /// <summary>One bad CV must not abandon the rest of the batch — the recruiter uploaded fifty
    /// and expects forty-nine of them to land.</summary>
    [Fact]
    public async Task One_Bad_File_Does_Not_Abandon_The_Others()
    {
        var fixture = BuildFixture();
        var tenantId = Guid.NewGuid();
        Seed(fixture, tenantId, fileNames: ["bad.pdf", "good.pdf"]);

        fixture.Extractor.Behaviour = name => name == "bad.pdf"
            ? throw new InvalidDataException("cannot parse this")
            : FakeExtractor.Result(name, "good@example.test", null);

        await fixture.Worker.RunOnceAsync();

        var files = fixture.Files();
        Assert.Equal(BulkFileStatus.Queued, files.Single(f => f.FileName == "bad.pdf").Status);
        Assert.Equal(BulkFileStatus.Success, files.Single(f => f.FileName == "good.pdf").Status);
    }

    // ---------------------------------------------------------------- storage & attribution

    /// <summary>The bytes are uploaded once and referenced, never copied. A second upload would
    /// double the storage bill for every CV in the system and leave two keys to keep in step.</summary>
    [Fact]
    public async Task The_Application_Reuses_The_Stored_Object_Rather_Than_Copying_It()
    {
        var fixture = BuildFixture();
        Seed(fixture, Guid.NewGuid(), fileNames: "reuse.pdf");

        await fixture.Worker.RunOnceAsync();

        var file = fixture.Files().Single();
        var application = fixture.Applications().Single();

        Assert.Equal(file.StorageKey, application.ResumeFileKey);
        Assert.Equal("reuse.pdf", application.ResumeFileName);
        Assert.True(await fixture.Storage.ExistsAsync(file.StorageKey));
    }

    /// <summary>A CV that will never become an application must not leave a candidate's personal
    /// data sitting in storage with nothing pointing at it (Module 7.4 retention).</summary>
    [Fact]
    public async Task A_Terminally_Failed_File_Does_Not_Leave_Its_Bytes_Behind()
    {
        var fixture = BuildFixture(o => o.MaxAttempts = 1);
        Seed(fixture, Guid.NewGuid(), fileNames: "doomed.pdf");

        var key = fixture.Files().Single().StorageKey;
        Assert.True(await fixture.Storage.ExistsAsync(key));

        fixture.Extractor.Behaviour = _ => throw new InvalidDataException("cannot parse this");
        await fixture.Worker.RunOnceAsync();

        Assert.Equal(BulkFileStatus.Failed, fixture.Files().Single().Status);
        Assert.False(await fixture.Storage.ExistsAsync(key));
    }

    /// <summary>ADR-0026 §4 — a job attributes what it writes to an explicit actor. The recruiter
    /// who uploaded the batch, recorded at upload time; not null, and not whoever happens to be
    /// around when the file is finally processed.</summary>
    [Fact]
    public async Task The_Stage_History_Is_Attributed_To_Whoever_Uploaded_The_Batch()
    {
        var fixture = BuildFixture();
        var uploaderId = Guid.NewGuid();
        Seed(fixture, Guid.NewGuid(), uploaderId, fileNames: "attributed.pdf");

        await fixture.Worker.RunOnceAsync();

        var history = fixture.History().Single();
        Assert.Equal(uploaderId, history.ChangedByUserId);
        Assert.Equal(PipelineStatus.Sourced, history.ToStatus);
        Assert.Null(history.FromStatus);
    }

    // ── A scanned PDF, with no OCR in this build (2026-08-29) ────────────────────────────
    //
    // Images are rejected at upload, but a scan is a PDF and cannot be told apart from a text
    // one until its stream comes back empty. Before this, the extractor answered that case with
    // a fabricated "Image Document: … | Dimensions: …" string, and the worker built a candidate
    // out of it: no name, no email, no phone, and a searchable resume text that was a
    // description of the file. It was reported to the recruiter as Success.

    private static DocumentExtractionResult NoTextFound() =>
        new(
            ExtractedText: string.Empty,
            OriginalText: string.Empty,
            DetectedLanguage: "en",
            IsZawgyiNormalized: false,
            ParsedContactInfo: new ParsedContactInfoDto(null, null, null, null, []));

    [Fact]
    public async Task A_File_With_No_Extractable_Text_Is_Skipped_Not_Failed()
    {
        var fixture = BuildFixture();
        fixture.Extractor.Behaviour = _ => NoTextFound();
        Seed(fixture, Guid.NewGuid(), fileNames: "scan.pdf");

        await fixture.Worker.RunOnceAsync();

        var file = fixture.Files().Single();
        // Skipped, not Failed: nothing went wrong and re-uploading the same bytes would behave
        // identically, so telling the recruiter to try again would be a lie.
        Assert.Equal(BulkFileStatus.Skipped, file.Status);
        Assert.NotNull(file.CompletedAt);
        // And the reason has to explain, since the recruiter can see it.
        Assert.Contains("text recognition is not enabled", file.LastError);
    }

    [Fact]
    public async Task A_Skipped_File_Creates_No_Candidate_And_No_Application()
    {
        var fixture = BuildFixture();
        fixture.Extractor.Behaviour = _ => NoTextFound();
        Seed(fixture, Guid.NewGuid(), fileNames: "photo-of-cv.pdf");

        await fixture.Worker.RunOnceAsync();

        // The whole point. A blank candidate is worse than no candidate: it occupies the
        // recruiter's pipeline, it deduplicates against nothing, and its resume text was
        // previously indexed by trigram search as "Image Document: …".
        Assert.Empty(fixture.Candidates());
        Assert.Empty(fixture.Applications());
        Assert.Empty(fixture.History());
    }

    [Fact]
    public async Task A_Skipped_File_Is_Not_Retried()
    {
        var fixture = BuildFixture();
        fixture.Extractor.Behaviour = _ => NoTextFound();
        Seed(fixture, Guid.NewGuid(), fileNames: "scan.pdf");

        await fixture.Worker.RunOnceAsync();
        var callsAfterFirst = fixture.Extractor.Calls;

        fixture.Clock.Advance(TimeSpan.FromHours(6));
        await fixture.Worker.RunOnceAsync();

        // Terminal. Backing a no-OCR file off and re-reading it every few minutes would burn the
        // queue on a file whose answer cannot change.
        Assert.Equal(callsAfterFirst, fixture.Extractor.Calls);
        Assert.Equal(BulkFileStatus.Skipped, fixture.Files().Single().Status);
    }

    [Fact]
    public async Task A_Skipped_File_Keeps_Its_Bytes()
    {
        var fixture = BuildFixture();
        fixture.Extractor.Behaviour = _ => NoTextFound();
        Seed(fixture, Guid.NewGuid(), fileNames: "scan.pdf");

        var key = fixture.Files().Single().StorageKey;
        Assert.False(string.IsNullOrWhiteSpace(key));

        await fixture.Worker.RunOnceAsync();

        // Unlike a Failed file, whose bytes are deleted because it will never become an
        // application. A skipped scan is a CV a real person sent; it stays, both because the
        // recruiter is told it was kept and because enabling OCR later should be able to read it.
        Assert.NotNull(await fixture.Storage.DownloadAsync(key!));
    }

    [Fact]
    public async Task One_Skipped_File_Does_Not_Stop_The_Rest_Of_The_Batch()
    {
        var fixture = BuildFixture();
        fixture.Extractor.Behaviour = fileName =>
            fileName == "scan.pdf" ? NoTextFound() : FakeExtractor.Result(fileName, $"{fileName}@example.test", null);

        Seed(fixture, Guid.NewGuid(), fileNames: new[] { "good-1.pdf", "scan.pdf", "good-2.pdf" });

        await fixture.Worker.RunOnceAsync();

        var files = fixture.Files();
        Assert.Equal(BulkFileStatus.Success, files.Single(f => f.FileName == "good-1.pdf").Status);
        Assert.Equal(BulkFileStatus.Skipped, files.Single(f => f.FileName == "scan.pdf").Status);
        Assert.Equal(BulkFileStatus.Success, files.Single(f => f.FileName == "good-2.pdf").Status);
        Assert.Equal(2, fixture.Applications().Count);
    }
}
