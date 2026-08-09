using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using RecruitOps.Api.Auth;
using RecruitOps.Application.DTOs;
using RecruitOps.Infrastructure.Services.DocumentExtraction;
using Xunit;

namespace RecruitOps.Api.Tests;

public class ResumeExtractionTests : IClassFixture<CustomWebAppFactory>
{
    private readonly Module3Scenario _scenario;

    public ResumeExtractionTests(CustomWebAppFactory factory)
    {
        _scenario = new Module3Scenario(factory);
    }

    private HttpClient Recruiter() => _scenario.Recruiter();

    private static byte[] CreateSampleDocxBytes(string contentText)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = zip.CreateEntry("word/document.xml");
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            writer.Write($@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<w:document xmlns:w=""http://schemas.openxmlformats.org/wordprocessingml/2006/main"">
  <w:body>
    <w:p><w:t>{contentText}</w:t></w:p>
  </w:body>
</w:document>");
        }
        return ms.ToArray();
    }

    [Fact]
    public async Task UploadResume_SuccessfulDocx_Returns200AndExtractedText()
    {
        var (_, appId) = await _scenario.ApplicationAsync("Resume Test Docx");
        var client = Recruiter();

        string sampleContent = "Aung Aung\nSoftware Engineer\nEmail: aung@example.com\nPhone: 09765432100\n5 years of experience\nSkills: React, JavaScript, SQL";
        byte[] docxBytes = CreateSampleDocxBytes(sampleContent);

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(docxBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        content.Add(fileContent, "file", "resume_aung.docx");

        var response = await client.PostAsync($"/api/applications/{appId}/resume", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ResumeExtractionResultDto>();

        Assert.NotNull(result);
        Assert.Equal(appId, result.ApplicationId);
        Assert.Equal("resume_aung.docx", result.FileName);
        Assert.Contains("aung@example.com", result.ExtractedText);
        Assert.Equal("aung@example.com", result.ParsedContactInfo.Email);
        Assert.Equal("09765432100", result.ParsedContactInfo.Phone);
        Assert.Equal(5, result.ParsedContactInfo.YearsOfExperience);
        Assert.Contains("React", result.ParsedContactInfo.Skills);
    }

    [Fact]
    public async Task UploadResume_SuccessfulPdfOrImage_Returns200AndResultDto()
    {
        var (_, appId) = await _scenario.ApplicationAsync("Resume Test PNG");
        var client = Recruiter();

        byte[] pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }; // PNG header

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(pngBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "scanned_cv.png");

        var response = await client.PostAsync($"/api/applications/{appId}/resume", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ResumeExtractionResultDto>();

        Assert.NotNull(result);
        Assert.Equal(appId, result.ApplicationId);
        Assert.Equal("scanned_cv.png", result.FileName);
        Assert.NotNull(result.ExtractedText);
    }

    [Fact]
    public async Task UploadResume_ZawgyiNormalization_NormalizesToUnicode()
    {
        var (_, appId) = await _scenario.ApplicationAsync("Resume Test Zawgyi");
        var client = Recruiter();

        // Zawgyi encoded text for မင်္ဂလာပါ (Zawgyi visual order: မၤဂလာပါ)
        string zawgyiText = "မၤဂလာပါ Aung Aung Email: zawgyi@example.com Phone: 09970123456";
        byte[] docxBytes = CreateSampleDocxBytes(zawgyiText);

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(docxBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        content.Add(fileContent, "file", "zawgyi_cv.docx");

        var response = await client.PostAsync($"/api/applications/{appId}/resume", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ResumeExtractionResultDto>();

        Assert.NotNull(result);
        Assert.True(result.IsZawgyiNormalized);
        Assert.Contains("မင်္ဂလာပါ", result.ExtractedText);
    }

    [Fact]
    public async Task UploadResume_FileExceeding10MB_Returns400BadRequest()
    {
        var client = Recruiter();
        var appId = Guid.NewGuid();

        using var content = new MultipartFormDataContent();
        byte[] largeBytes = new byte[11 * 1024 * 1024]; // 11MB
        var fileContent = new ByteArrayContent(largeBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", "too_large.pdf");

        var response = await client.PostAsync($"/api/applications/{appId}/resume", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadResume_InvalidFileFormat_Returns400BadRequest()
    {
        var client = Recruiter();
        var appId = Guid.NewGuid();

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("echo hello"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/x-msdownload");
        content.Add(fileContent, "file", "script.exe");

        var response = await client.PostAsync($"/api/applications/{appId}/resume", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadResume_ApplicationNotFound_Returns404NotFound()
    {
        var client = Recruiter();
        var nonExistentAppId = Guid.NewGuid();

        byte[] docxBytes = CreateSampleDocxBytes("Sample text");
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(docxBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        content.Add(fileContent, "file", "cv.docx");

        var response = await client.PostAsync($"/api/applications/{nonExistentAppId}/resume", content);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetResume_UploadedResume_ReturnsFileStream()
    {
        var (_, appId) = await _scenario.ApplicationAsync("Resume Test Get");
        var client = Recruiter();

        string sampleContent = "Resume content for download test";
        byte[] docxBytes = CreateSampleDocxBytes(sampleContent);

        using var uploadContent = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(docxBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        uploadContent.Add(fileContent, "file", "my_resume.docx");

        var uploadResponse = await client.PostAsync($"/api/applications/{appId}/resume", uploadContent);
        uploadResponse.EnsureSuccessStatusCode();

        var downloadResponse = await client.GetAsync($"/api/applications/{appId}/resume");

        Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);
        byte[] downloadedBytes = await downloadResponse.Content.ReadAsByteArrayAsync();
        Assert.NotEmpty(downloadedBytes);
    }

    [Fact]
    public void DocumentTextExtractor_ParsesContactInfoHeuristics()
    {
        string rawText = @"Maung Maung
Full Stack Developer
Email: maung.dev@example.com
Phone: +95 9 1234 5678
Total 7 years of experience in enterprise software.
Technical Skills: C#, .NET, ASP.NET, React, TypeScript, PostgreSQL, Docker, AWS";

        var parsed = DocumentTextExtractor.ExtractContactInfo(rawText);

        Assert.Equal("Maung Maung", parsed.CandidateName);
        Assert.Equal("maung.dev@example.com", parsed.Email);
        Assert.Equal("+95 9 1234 5678", parsed.Phone);
        Assert.Equal(7, parsed.YearsOfExperience);
        Assert.Contains("C#", parsed.Skills);
        Assert.Contains(".NET", parsed.Skills);
        Assert.Contains("React", parsed.Skills);
        Assert.Contains("Docker", parsed.Skills);
    }
}
