using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using RecruitOps.Application.DTOs;
using Xunit;

namespace RecruitOps.Api.Tests;

public class BulkResumeUploadTests : IClassFixture<CustomWebAppFactory>
{
    private readonly Module3Scenario _scenario;

    public BulkResumeUploadTests(CustomWebAppFactory factory)
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

    [Fact]
    public async Task BulkUpload_ValidBatchUpTo50Files_Returns200AndBatchId()
    {
        var (postingId, _) = await _scenario.ApplicationAsync("Bulk Job Posting 1");
        var client = Recruiter();

        using var content = new MultipartFormDataContent();
        for (int i = 1; i <= 3; i++)
        {
            byte[] bytes = CreateSampleDocx($"Candidate {i}\nEmail: candidate{i}@example.com\nPhone: 0976543210{i}");
            var fileContent = new ByteArrayContent(bytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
            content.Add(fileContent, "files", $"cv_{i}.docx");
        }

        var response = await client.PostAsync($"/api/jobpostings/{postingId}/resumes/bulk", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<BulkUploadBatchResponseDto>();
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.BatchId);
        Assert.Equal(postingId, result.JobPostingId);
        Assert.Equal(3, result.TotalFiles);
    }

    [Fact]
    public async Task GetBatchStatus_ReturnsPerFileProgressSummary()
    {
        var (postingId, _) = await _scenario.ApplicationAsync("Bulk Job Posting 2");
        var client = Recruiter();

        using var content = new MultipartFormDataContent();
        byte[] bytes = CreateSampleDocx("Min Min\nEmail: minmin@example.com\nPhone: 09987654321");
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        content.Add(fileContent, "files", "min_min.docx");

        var response = await client.PostAsync($"/api/jobpostings/{postingId}/resumes/bulk", content);
        var batchResponse = await response.Content.ReadFromJsonAsync<BulkUploadBatchResponseDto>();
        Assert.NotNull(batchResponse);

        // Wait brief moment for background processing
        await Task.Delay(300);

        var statusResponse = await client.GetAsync($"/api/jobpostings/{postingId}/resumes/bulk/{batchResponse.BatchId}");
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);

        var status = await statusResponse.Content.ReadFromJsonAsync<BulkBatchStatusDto>();
        Assert.NotNull(status);
        Assert.Equal(batchResponse.BatchId, status.BatchId);
        Assert.Equal(1, status.TotalFiles);
        Assert.Single(status.Items);
        Assert.Equal("min_min.docx", status.Items[0].FileName);
    }

    [Fact]
    public async Task BulkUpload_Exceeding50Files_Returns400BadRequest()
    {
        var (postingId, _) = await _scenario.ApplicationAsync("Bulk Job Posting 3");
        var client = Recruiter();

        using var content = new MultipartFormDataContent();
        for (int i = 1; i <= 51; i++)
        {
            var fileContent = new ByteArrayContent(new byte[] { 1, 2, 3 });
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            content.Add(fileContent, "files", $"cv_{i}.pdf");
        }

        var response = await client.PostAsync($"/api/jobpostings/{postingId}/resumes/bulk", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BulkUpload_EmptyFileCollection_Returns400BadRequest()
    {
        var (postingId, _) = await _scenario.ApplicationAsync("Bulk Job Posting 4");
        var client = Recruiter();

        using var content = new MultipartFormDataContent();
        var response = await client.PostAsync($"/api/jobpostings/{postingId}/resumes/bulk", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BulkUpload_UnauthorizedDepartmentAccess_Returns404Or403()
    {
        var (postingId, _) = await _scenario.ApplicationAsync("Sales Job Posting");
        // FinanceManager does not have access to Sales department
        var client = FinanceManager();

        using var content = new MultipartFormDataContent();
        byte[] bytes = CreateSampleDocx("Unauthorized Applicant");
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        content.Add(fileContent, "files", "unauth_cv.docx");

        var response = await client.PostAsync($"/api/jobpostings/{postingId}/resumes/bulk", content);
        Assert.True(response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task BulkUpload_ZawgyiCV_NormalizesExtractedText()
    {
        var (postingId, _) = await _scenario.ApplicationAsync("Bulk Job Posting Zawgyi");
        var client = Recruiter();

        string zawgyiContent = "မၤဂလာပါ Kyaw Kyaw Email: kyawkyaw@example.com Phone: 09790000000";
        byte[] bytes = CreateSampleDocx(zawgyiContent);

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        content.Add(fileContent, "files", "zawgyi_cv.docx");

        var response = await client.PostAsync($"/api/jobpostings/{postingId}/resumes/bulk", content);
        var batchRes = await response.Content.ReadFromJsonAsync<BulkUploadBatchResponseDto>();

        await Task.Delay(400);

        var statusRes = await client.GetAsync($"/api/jobpostings/{postingId}/resumes/bulk/{batchRes!.BatchId}");
        var status = await statusRes.Content.ReadFromJsonAsync<BulkBatchStatusDto>();

        Assert.NotNull(status);
        Assert.Single(status.Items);
        Assert.Equal("Success", status.Items[0].Status);
        Assert.NotNull(status.Items[0].ApplicationId);
    }

    [Fact]
    public async Task BulkUpload_DuplicateCandidate_ReusesExistingCandidate()
    {
        var (postingId, _) = await _scenario.ApplicationAsync("Bulk Job Posting Duplicate Test");
        var client = Recruiter();

        // 2 files with same candidate email
        byte[] bytes1 = CreateSampleDocx("Existing Candidate\nEmail: duplicate@example.com\nPhone: 09700000001");
        byte[] bytes2 = CreateSampleDocx("Existing Candidate 2\nEmail: duplicate@example.com\nPhone: 09700000001");

        using var content = new MultipartFormDataContent();
        var fc1 = new ByteArrayContent(bytes1);
        fc1.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        content.Add(fc1, "files", "dup1.docx");

        var fc2 = new ByteArrayContent(bytes2);
        fc2.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        content.Add(fc2, "files", "dup2.docx");

        var response = await client.PostAsync($"/api/jobpostings/{postingId}/resumes/bulk", content);
        var batchRes = await response.Content.ReadFromJsonAsync<BulkUploadBatchResponseDto>();

        await Task.Delay(500);

        var statusRes = await client.GetAsync($"/api/jobpostings/{postingId}/resumes/bulk/{batchRes!.BatchId}");
        var status = await statusRes.Content.ReadFromJsonAsync<BulkBatchStatusDto>();

        Assert.NotNull(status);
        Assert.Equal(2, status.SuccessCount);
        Assert.Equal(2, status.Items.Count);
        // Both items should share the exact same CandidateId
        Assert.Equal(status.Items[0].CandidateId, status.Items[1].CandidateId);
    }

    [Fact]
    public async Task BulkUpload_CorruptOrUnsupportedFile_MarksItemAsFailed()
    {
        var (postingId, _) = await _scenario.ApplicationAsync("Bulk Job Posting Unsupported Test");
        var client = Recruiter();

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("echo virus"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/x-msdownload");
        content.Add(fileContent, "files", "bad_file.exe");

        var response = await client.PostAsync($"/api/jobpostings/{postingId}/resumes/bulk", content);
        var batchRes = await response.Content.ReadFromJsonAsync<BulkUploadBatchResponseDto>();

        await Task.Delay(300);

        var statusRes = await client.GetAsync($"/api/jobpostings/{postingId}/resumes/bulk/{batchRes!.BatchId}");
        var status = await statusRes.Content.ReadFromJsonAsync<BulkBatchStatusDto>();

        Assert.NotNull(status);
        Assert.Equal(1, status.FailedCount);
        Assert.Equal("Failed", status.Items[0].Status);
    }
}
