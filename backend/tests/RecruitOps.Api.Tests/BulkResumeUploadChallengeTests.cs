using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using RecruitOps.Application.DTOs;
using Xunit;

namespace RecruitOps.Api.Tests;

public class BulkResumeUploadChallengeTests : IClassFixture<CustomWebAppFactory>
{
    private readonly Module3Scenario _scenario;

    public BulkResumeUploadChallengeTests(CustomWebAppFactory factory)
    {
        _scenario = new Module3Scenario(factory);
    }

    private HttpClient Recruiter() => _scenario.Recruiter();
    private HttpClient FinanceManager() => _scenario.FinanceManager();

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

    #region 1. Status Polling Tests

    [Fact]
    public async Task StatusPolling_NonExistentBatchId_Returns404NotFound()
    {
        var (postingId, _) = await _scenario.ApplicationAsync("Challenge Polling Posting 1");
        var client = Recruiter();

        var randomBatchId = Guid.NewGuid();
        var response = await client.GetAsync($"/api/jobpostings/{postingId}/resumes/bulk/{randomBatchId}");
        
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task StatusPolling_WrongJobPostingId_Returns404NotFound()
    {
        var (postingId1, _) = await _scenario.ApplicationAsync("Challenge Polling Posting 2A");
        var (postingId2, _) = await _scenario.ApplicationAsync("Challenge Polling Posting 2B");
        var client = Recruiter();

        // Enqueue batch for posting 1
        using var content = new MultipartFormDataContent();
        byte[] bytes = CreateSampleDocx("Test Candidate\nEmail: candidate_poll@example.com");
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        content.Add(fileContent, "files", "cv_poll.docx");

        var uploadResp = await client.PostAsync($"/api/jobpostings/{postingId1}/resumes/bulk", content);
        var batchRes = await uploadResp.Content.ReadFromJsonAsync<BulkUploadBatchResponseDto>();
        Assert.NotNull(batchRes);

        // Polling with posting 2 ID should return 404 NotFound
        var response = await client.GetAsync($"/api/jobpostings/{postingId2}/resumes/bulk/{batchRes.BatchId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task StatusPolling_CompletedBatch_ReturnsCompletedStatusWithFullSummary()
    {
        var (postingId, _) = await _scenario.ApplicationAsync("Challenge Polling Posting 3");
        var client = Recruiter();

        using var content = new MultipartFormDataContent();
        byte[] bytes = CreateSampleDocx("Completed Candidate\nEmail: poll_completed@example.com");
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        content.Add(fileContent, "files", "cv_completed.docx");

        var uploadResp = await client.PostAsync($"/api/jobpostings/{postingId}/resumes/bulk", content);
        var batchRes = await uploadResp.Content.ReadFromJsonAsync<BulkUploadBatchResponseDto>();
        Assert.NotNull(batchRes);

        // Wait for asynchronous processing to complete
        await Task.Delay(500);

        var response = await client.GetAsync($"/api/jobpostings/{postingId}/resumes/bulk/{batchRes.BatchId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var status = await response.Content.ReadFromJsonAsync<BulkBatchStatusDto>();
        Assert.NotNull(status);
        Assert.Equal("Completed", status.Status);
        Assert.Equal(1, status.TotalFiles);
        Assert.Equal(1, status.ProcessedFiles);
        Assert.Equal(1, status.SuccessCount);
        Assert.Equal(0, status.FailedCount);
        Assert.NotNull(status.CompletedAt);
        Assert.Single(status.Items);
        Assert.Equal("Success", status.Items[0].Status);
        Assert.NotNull(status.Items[0].ApplicationId);
        Assert.NotNull(status.Items[0].CandidateId);
    }

    #endregion

    #region 2. Department Authorization Isolation Tests

    [Fact]
    public async Task AuthorizationIsolation_UserFromOtherDepartment_BulkUpload_Returns403Or404()
    {
        // Job posting is created in Sales department (accessible by Recruiter, but not FinanceManager)
        var (postingId, _) = await _scenario.ApplicationAsync("Sales Dept Posting 1");
        var unauthorizedClient = FinanceManager();

        using var content = new MultipartFormDataContent();
        byte[] bytes = CreateSampleDocx("Dept A Intruder\nEmail: intruder@example.com");
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        content.Add(fileContent, "files", "intruder.docx");

        var response = await unauthorizedClient.PostAsync($"/api/jobpostings/{postingId}/resumes/bulk", content);
        Assert.True(response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.Forbidden,
            $"Expected 404 or 403, but got {response.StatusCode}");
    }

    [Fact]
    public async Task AuthorizationIsolation_UserFromOtherDepartment_GetBatchStatus_Returns403Or404()
    {
        var (postingId, _) = await _scenario.ApplicationAsync("Sales Dept Posting 2");
        var authorizedClient = Recruiter();
        var unauthorizedClient = FinanceManager();

        // Recruiter posts valid batch
        using var content = new MultipartFormDataContent();
        byte[] bytes = CreateSampleDocx("Sales Candidate\nEmail: sales_cand@example.com");
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        content.Add(fileContent, "files", "sales_cv.docx");

        var uploadResp = await authorizedClient.PostAsync($"/api/jobpostings/{postingId}/resumes/bulk", content);
        var batchRes = await uploadResp.Content.ReadFromJsonAsync<BulkUploadBatchResponseDto>();
        Assert.NotNull(batchRes);

        // Unauthorized user attempts to view batch status of Sales posting
        var response = await unauthorizedClient.GetAsync($"/api/jobpostings/{postingId}/resumes/bulk/{batchRes.BatchId}");
        Assert.True(response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.Forbidden,
            $"Expected 404 or 403, but got {response.StatusCode}");
    }

    #endregion

    #region 3. Candidate Deduplication Tests

    [Fact]
    public async Task CandidateDeduplication_WithinSameBatch_DuplicateEmail_ReusesCandidate()
    {
        var (postingId, _) = await _scenario.ApplicationAsync("Deduplication Posting 1");
        var client = Recruiter();

        string sharedEmail = "same_batch_dedup@example.com";
        byte[] bytes1 = CreateSampleDocx($"John Doe 1\nEmail: {sharedEmail}\nPhone: 09111111111");
        byte[] bytes2 = CreateSampleDocx($"John Doe 2\nEmail: {sharedEmail}\nPhone: 09222222222");

        using var content = new MultipartFormDataContent();
        var fc1 = new ByteArrayContent(bytes1);
        fc1.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        content.Add(fc1, "files", "resume1.docx");

        var fc2 = new ByteArrayContent(bytes2);
        fc2.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        content.Add(fc2, "files", "resume2.docx");

        var uploadResp = await client.PostAsync($"/api/jobpostings/{postingId}/resumes/bulk", content);
        var batchRes = await uploadResp.Content.ReadFromJsonAsync<BulkUploadBatchResponseDto>();
        Assert.NotNull(batchRes);

        await Task.Delay(500);

        var statusResp = await client.GetAsync($"/api/jobpostings/{postingId}/resumes/bulk/{batchRes.BatchId}");
        var status = await statusResp.Content.ReadFromJsonAsync<BulkBatchStatusDto>();

        Assert.NotNull(status);
        Assert.Equal(2, status.SuccessCount);
        Assert.Equal(status.Items[0].CandidateId, status.Items[1].CandidateId);
    }

    [Fact]
    public async Task CandidateDeduplication_AcrossBatches_DuplicateEmail_ReusesCandidate()
    {
        var (postingId1, _) = await _scenario.ApplicationAsync("Deduplication Posting 2A");
        var (postingId2, _) = await _scenario.ApplicationAsync("Deduplication Posting 2B");
        var client = Recruiter();

        string sharedEmail = "cross_batch_dedup@example.com";

        // Batch 1
        using (var content1 = new MultipartFormDataContent())
        {
            byte[] bytes1 = CreateSampleDocx($"Alice Cross\nEmail: {sharedEmail}\nPhone: 09333333333");
            var fc1 = new ByteArrayContent(bytes1);
            fc1.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
            content1.Add(fc1, "files", "batch1.docx");

            var uploadResp1 = await client.PostAsync($"/api/jobpostings/{postingId1}/resumes/bulk", content1);
            var batchRes1 = await uploadResp1.Content.ReadFromJsonAsync<BulkUploadBatchResponseDto>();
            Assert.NotNull(batchRes1);

            await Task.Delay(500);

            var statusResp1 = await client.GetAsync($"/api/jobpostings/{postingId1}/resumes/bulk/{batchRes1.BatchId}");
            var status1 = await statusResp1.Content.ReadFromJsonAsync<BulkBatchStatusDto>();
            Assert.NotNull(status1);
            Assert.Equal("Success", status1.Items[0].Status);

            Guid candidateId1 = status1.Items[0].CandidateId!.Value;

            // Batch 2 to a different job posting
            using (var content2 = new MultipartFormDataContent())
            {
                byte[] bytes2 = CreateSampleDocx($"Alice Cross Updated\nEmail: {sharedEmail}\nPhone: 09444444444");
                var fc2 = new ByteArrayContent(bytes2);
                fc2.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
                content2.Add(fc2, "files", "batch2.docx");

                var uploadResp2 = await client.PostAsync($"/api/jobpostings/{postingId2}/resumes/bulk", content2);
                var batchRes2 = await uploadResp2.Content.ReadFromJsonAsync<BulkUploadBatchResponseDto>();
                Assert.NotNull(batchRes2);

                await Task.Delay(500);

                var statusResp2 = await client.GetAsync($"/api/jobpostings/{postingId2}/resumes/bulk/{batchRes2.BatchId}");
                var status2 = await statusResp2.Content.ReadFromJsonAsync<BulkBatchStatusDto>();
                Assert.NotNull(status2);
                Assert.Equal("Success", status2.Items[0].Status);

                Guid candidateId2 = status2.Items[0].CandidateId!.Value;

                Assert.Equal(candidateId1, candidateId2);
            }
        }
    }

    [Fact]
    public async Task CandidateDeduplication_ByPhoneOnly_ReusesCandidate()
    {
        var (postingId, _) = await _scenario.ApplicationAsync("Deduplication Posting Phone");
        var client = Recruiter();

        string sharedPhone = "09777788889";
        byte[] bytes1 = CreateSampleDocx($"Bob Phone\nPhone: {sharedPhone}");
        byte[] bytes2 = CreateSampleDocx($"Bob Phone 2\nPhone: {sharedPhone}");

        using var content = new MultipartFormDataContent();
        var fc1 = new ByteArrayContent(bytes1);
        fc1.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        content.Add(fc1, "files", "phone1.docx");

        var fc2 = new ByteArrayContent(bytes2);
        fc2.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        content.Add(fc2, "files", "phone2.docx");

        var uploadResp = await client.PostAsync($"/api/jobpostings/{postingId}/resumes/bulk", content);
        var batchRes = await uploadResp.Content.ReadFromJsonAsync<BulkUploadBatchResponseDto>();
        Assert.NotNull(batchRes);

        await Task.Delay(500);

        var statusResp = await client.GetAsync($"/api/jobpostings/{postingId}/resumes/bulk/{batchRes.BatchId}");
        var status = await statusResp.Content.ReadFromJsonAsync<BulkBatchStatusDto>();

        Assert.NotNull(status);
        Assert.Equal(2, status.SuccessCount);
        Assert.Equal(status.Items[0].CandidateId, status.Items[1].CandidateId);
    }

    #endregion

    #region 4. Stress & Mixed Batch Handling Tests

    [Fact]
    public async Task BulkUpload_MixedValidAndInvalidFiles_ProcessesValidAndFailsInvalid()
    {
        var (postingId, _) = await _scenario.ApplicationAsync("Mixed Batch Posting");
        var client = Recruiter();

        using var content = new MultipartFormDataContent();

        // 1. Valid file
        byte[] validBytes = CreateSampleDocx("Valid Candidate\nEmail: valid_mixed@example.com");
        var fcValid = new ByteArrayContent(validBytes);
        fcValid.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        content.Add(fcValid, "files", "valid.docx");

        // 2. Invalid file type (.exe)
        var fcInvalidExt = new ByteArrayContent(new byte[] { 0x4D, 0x5A });
        fcInvalidExt.Headers.ContentType = new MediaTypeHeaderValue("application/x-msdownload");
        content.Add(fcInvalidExt, "files", "forbidden.exe");

        // 3. Oversized file (>10MB)
        var fcOversized = new ByteArrayContent(new byte[10 * 1024 * 1024 + 1]);
        fcOversized.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fcOversized, "files", "large.pdf");

        var uploadResp = await client.PostAsync($"/api/jobpostings/{postingId}/resumes/bulk", content);
        var batchRes = await uploadResp.Content.ReadFromJsonAsync<BulkUploadBatchResponseDto>();
        Assert.NotNull(batchRes);

        await Task.Delay(600);

        var statusResp = await client.GetAsync($"/api/jobpostings/{postingId}/resumes/bulk/{batchRes.BatchId}");
        var status = await statusResp.Content.ReadFromJsonAsync<BulkBatchStatusDto>();

        Assert.NotNull(status);
        Assert.Equal(3, status.TotalFiles);
        Assert.Equal(3, status.ProcessedFiles);
        Assert.Equal(1, status.SuccessCount);
        Assert.Equal(2, status.FailedCount);
        Assert.Equal("Completed", status.Status);
    }

    #endregion
}
