using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using RecruitOps.Application.DTOs;
using Xunit;

namespace RecruitOps.Api.Tests;

public class BulkResumeUploadStressTests : IClassFixture<CustomWebAppFactory>
{
    private readonly Module3Scenario _scenario;

    public BulkResumeUploadStressTests(CustomWebAppFactory factory)
    {
        _scenario = new Module3Scenario(factory);
    }

    private HttpClient Recruiter() => _scenario.Recruiter();

    private static byte[] CreateSampleDocx(string contentText)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = zip.CreateEntry("word/document.xml");
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            writer.Write($@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<w:document xmlns:w=""http://schemas.openxmlformats.org/wordprocessingml/2006/main"">
  <w:body><w:p><w:t>{contentText}</w:t></w:p></w:body>
</w:document>");
        }
        return ms.ToArray();
    }

    [Fact]
    public async Task BoundaryTest_0_1_50_51_Files()
    {
        var (postingId, _) = await _scenario.ApplicationAsync("Boundary Test Job Posting");
        var client = Recruiter();

        // 1. Boundary: 0 files -> 400 BadRequest
        using (var emptyContent = new MultipartFormDataContent())
        {
            var res0 = await client.PostAsync($"/api/jobpostings/{postingId}/resumes/bulk", emptyContent);
            Assert.Equal(HttpStatusCode.BadRequest, res0.StatusCode);
        }

        // 2. Boundary: 1 file -> 200 OK & completes successfully
        using (var content1 = new MultipartFormDataContent())
        {
            byte[] bytes = CreateSampleDocx("Boundary Candidate 1\nEmail: boundary1@example.com");
            var fc = new ByteArrayContent(bytes);
            fc.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
            content1.Add(fc, "files", "single_cv.docx");

            var res1 = await client.PostAsync($"/api/jobpostings/{postingId}/resumes/bulk", content1);
            Assert.Equal(HttpStatusCode.OK, res1.StatusCode);
            var batch1 = await res1.Content.ReadFromJsonAsync<BulkUploadBatchResponseDto>();
            Assert.NotNull(batch1);
            Assert.Equal(1, batch1.TotalFiles);

            await Task.Delay(300);
            var statusRes1 = await client.GetAsync($"/api/jobpostings/{postingId}/resumes/bulk/{batch1.BatchId}");
            var status1 = await statusRes1.Content.ReadFromJsonAsync<BulkBatchStatusDto>();
            Assert.NotNull(status1);
            Assert.Equal(1, status1.SuccessCount);
        }

        // 3. Boundary: 50 files -> 200 OK & completes successfully
        using (var content50 = new MultipartFormDataContent())
        {
            for (int i = 1; i <= 50; i++)
            {
                byte[] bytes = CreateSampleDocx($"Boundary Candidate Batch {i}\nEmail: boundarybatch{i}@example.com");
                var fc = new ByteArrayContent(bytes);
                fc.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
                content50.Add(fc, "files", $"batch50_cv_{i}.docx");
            }

            var res50 = await client.PostAsync($"/api/jobpostings/{postingId}/resumes/bulk", content50);
            Assert.Equal(HttpStatusCode.OK, res50.StatusCode);
            var batch50 = await res50.Content.ReadFromJsonAsync<BulkUploadBatchResponseDto>();
            Assert.NotNull(batch50);
            Assert.Equal(50, batch50.TotalFiles);

            // Wait for 50 files to finish processing asynchronously
            int retries = 0;
            BulkBatchStatusDto? status50 = null;
            while (retries < 20)
            {
                await Task.Delay(300);
                var statusRes50 = await client.GetAsync($"/api/jobpostings/{postingId}/resumes/bulk/{batch50.BatchId}");
                status50 = await statusRes50.Content.ReadFromJsonAsync<BulkBatchStatusDto>();
                if (status50 != null && status50.ProcessedFiles == 50) break;
                retries++;
            }

            Assert.NotNull(status50);
            Assert.Equal(50, status50.ProcessedFiles);
            Assert.Equal(50, status50.SuccessCount);
            Assert.Equal("Completed", status50.Status);
        }

        // 4. Boundary: 51 files -> 400 BadRequest
        using (var content51 = new MultipartFormDataContent())
        {
            for (int i = 1; i <= 51; i++)
            {
                var fc = new ByteArrayContent(new byte[] { 1, 2, 3 });
                fc.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
                content51.Add(fc, "files", $"cv_{i}.pdf");
            }

            var res51 = await client.PostAsync($"/api/jobpostings/{postingId}/resumes/bulk", content51);
            Assert.Equal(HttpStatusCode.BadRequest, res51.StatusCode);
        }
    }

    [Fact]
    public async Task InvalidExtensions_Oversized_Empty_CorruptFiles_HandledGracefully()
    {
        var (postingId, _) = await _scenario.ApplicationAsync("Edge Case Files Posting");
        var client = Recruiter();

        using var content = new MultipartFormDataContent();

        // 1. Invalid extension: .exe
        var exeFc = new ByteArrayContent(Encoding.UTF8.GetBytes("echo hello"));
        exeFc.Headers.ContentType = new MediaTypeHeaderValue("application/x-msdownload");
        content.Add(exeFc, "files", "script.exe");

        // 2. Invalid extension: .txt
        var txtFc = new ByteArrayContent(Encoding.UTF8.GetBytes("Plain text content"));
        txtFc.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(txtFc, "files", "document.txt");

        // 3. Invalid extension: .zip
        var zipFc = new ByteArrayContent(new byte[] { 0x50, 0x4B, 0x03, 0x04 });
        zipFc.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        content.Add(zipFc, "files", "archive.zip");

        // 4. Oversized file (>10MB: 10.5 MB)
        byte[] oversizedBytes = new byte[10 * 1024 * 1024 + 512 * 1024];
        var overFc = new ByteArrayContent(oversizedBytes);
        overFc.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(overFc, "files", "huge_cv.pdf");

        // 5. Empty file (0 bytes)
        var emptyFc = new ByteArrayContent(Array.Empty<byte>());
        emptyFc.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(emptyFc, "files", "empty_cv.pdf");

        // 6. Corrupt PDF file
        var corruptPdfFc = new ByteArrayContent(Encoding.UTF8.GetBytes("%PDF-1.4\ncorrupt garbage bytes binary null \0\0\0 header end"));
        corruptPdfFc.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(corruptPdfFc, "files", "corrupt.pdf");

        // 7. Corrupt DOCX file
        var corruptDocxFc = new ByteArrayContent(Encoding.UTF8.GetBytes("PK\x03\x04 corrupt docx binary structure"));
        corruptDocxFc.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        content.Add(corruptDocxFc, "files", "corrupt.docx");

        // 8. Valid DOCX file alongside edge cases
        byte[] validDocx = CreateSampleDocx("Valid Candidate\nEmail: validcandidate@example.com");
        var validFc = new ByteArrayContent(validDocx);
        validFc.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        content.Add(validFc, "files", "valid_candidate.docx");

        var response = await client.PostAsync($"/api/jobpostings/{postingId}/resumes/bulk", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var batchRes = await response.Content.ReadFromJsonAsync<BulkUploadBatchResponseDto>();
        Assert.NotNull(batchRes);
        Assert.Equal(8, batchRes.TotalFiles);

        // Poll for processing completion
        int retries = 0;
        BulkBatchStatusDto? status = null;
        while (retries < 20)
        {
            await Task.Delay(300);
            var statusRes = await client.GetAsync($"/api/jobpostings/{postingId}/resumes/bulk/{batchRes.BatchId}");
            status = await statusRes.Content.ReadFromJsonAsync<BulkBatchStatusDto>();
            if (status != null && status.ProcessedFiles == 8) break;
            retries++;
        }

        Assert.NotNull(status);
        Assert.Equal(8, status.ProcessedFiles);
        Assert.Equal("Completed", status.Status);

        var items = status.Items.ToDictionary(i => i.FileName);

        // Verify .exe failed
        Assert.Equal("Failed", items["script.exe"].Status);
        Assert.Contains("Unsupported file extension", items["script.exe"].ErrorMessage);

        // Verify .txt failed
        Assert.Equal("Failed", items["document.txt"].Status);
        Assert.Contains("Unsupported file extension", items["document.txt"].ErrorMessage);

        // Verify .zip failed
        Assert.Equal("Failed", items["archive.zip"].Status);
        Assert.Contains("Unsupported file extension", items["archive.zip"].ErrorMessage);

        // Verify oversized failed
        Assert.Equal("Failed", items["huge_cv.pdf"].Status);
        Assert.Contains("exceeds maximum limit", items["huge_cv.pdf"].ErrorMessage);

        // Verify empty file failed
        Assert.Equal("Failed", items["empty_cv.pdf"].Status);

        // Verify corrupt PDF handled (either processed or failed cleanly without crashing batch)
        Assert.NotNull(items["corrupt.pdf"].Status);

        // Verify corrupt DOCX handled
        Assert.NotNull(items["corrupt.docx"].Status);

        // Verify valid file succeeded despite other failing files in same batch
        Assert.Equal("Success", items["valid_candidate.docx"].Status);
        Assert.NotNull(items["valid_candidate.docx"].ApplicationId);
    }

    [Fact]
    public async Task ConcurrentBatchProcessing_ThreadSafetyTest()
    {
        var (postingId, _) = await _scenario.ApplicationAsync("Concurrent Stress Posting");
        var client = Recruiter();

        const int ConcurrentBatchCount = 10;
        var postTasks = new List<Task<HttpResponseMessage>>();

        for (int b = 1; b <= ConcurrentBatchCount; b++)
        {
            int batchNumber = b;
            postTasks.Add(Task.Run(async () =>
            {
                using var content = new MultipartFormDataContent();
                for (int f = 1; f <= 3; f++)
                {
                    byte[] docx = CreateSampleDocx($"Concurrent Cand {batchNumber}_{f}\nEmail: conc_{batchNumber}_{f}@example.com\nPhone: 09700{batchNumber}00{f}");
                    var fc = new ByteArrayContent(docx);
                    fc.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
                    content.Add(fc, "files", $"conc_b{batchNumber}_f{f}.docx");
                }

                return await client.PostAsync($"/api/jobpostings/{postingId}/resumes/bulk", content);
            }));
        }

        var responses = await Task.WhenAll(postTasks);

        var batchIds = new List<Guid>();
        foreach (var resp in responses)
        {
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            var batchDto = await resp.Content.ReadFromJsonAsync<BulkUploadBatchResponseDto>();
            Assert.NotNull(batchDto);
            batchIds.Add(batchDto.BatchId);
        }

        // All batch IDs must be unique
        Assert.Equal(ConcurrentBatchCount, batchIds.Distinct().Count());

        // Wait for all 10 concurrent batches to complete processing
        int totalProcessedBatches = 0;
        int retries = 0;
        while (retries < 25 && totalProcessedBatches < ConcurrentBatchCount)
        {
            await Task.Delay(400);
            totalProcessedBatches = 0;
            foreach (var bId in batchIds)
            {
                var statusRes = await client.GetAsync($"/api/jobpostings/{postingId}/resumes/bulk/{bId}");
                var status = await statusRes.Content.ReadFromJsonAsync<BulkBatchStatusDto>();
                if (status != null && status.Status == "Completed" && status.ProcessedFiles == 3)
                {
                    totalProcessedBatches++;
                }
            }
            retries++;
        }

        Assert.Equal(ConcurrentBatchCount, totalProcessedBatches);
    }
}
