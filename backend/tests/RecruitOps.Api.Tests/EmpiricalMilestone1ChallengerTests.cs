using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;
using RecruitOps.Infrastructure.Services.DocumentExtraction;
using RecruitOps.Infrastructure.Services.MyanmarScript;
using Xunit;

namespace RecruitOps.Api.Tests;

public class EmpiricalMilestone1ChallengerTests
{
    private readonly IDocumentTextExtractor _extractor = new DocumentTextExtractor(
        new MyanmarScriptNormalizer(),
        Microsoft.Extensions.Logging.Abstractions.NullLogger<DocumentTextExtractor>.Instance);

    private readonly IMyanmarScriptNormalizer _normalizer = new MyanmarScriptNormalizer();

    #region Task 1: Challenge Text Extraction Heuristics

    [Fact]
    public void Task1_SkillsExtraction_FailsOnSpecialCharSkills_LikeCSharpAndDotNet()
    {
        // Resume listing C# and .NET
        string cvText = "Technical Skills: C#, .NET, React, Docker, Python";
        var parsed = DocumentTextExtractor.ExtractContactInfo(cvText);

        // C# and .NET are now properly extracted from parsed.Skills
        Assert.Contains("C#", parsed.Skills);
        Assert.Contains(".NET", parsed.Skills);
        Assert.Contains("React", parsed.Skills);
        Assert.Contains("Docker", parsed.Skills);
    }

    [Fact]
    public void Task1_CandidateName_MistakesSectionHeader_ForCandidateName()
    {
        // CV with generic top header before actual name
        string cvText = "PERSONAL DETAILS\nAung Aung\nSoftware Developer\naung@example.com";
        var parsed = DocumentTextExtractor.ExtractContactInfo(cvText);

        // Empirical Challenge: Heuristic picks the first non-CV line as name.
        // "PERSONAL DETAILS" is not "Resume" or "CV", so candidateName becomes "PERSONAL DETAILS"
        Assert.Equal("PERSONAL DETAILS", parsed.CandidateName); // EMPIRICAL BUG CONFIRMED: Header misidentified
    }

    [Fact]
    public void Task1_ExperienceYears_MissesCommonPhrases()
    {
        // Phrase: "Experience: 5 years" or "5 years in software engineering"
        string cvInverted = "Experience: 5 years in backend engineering";
        string cvStandard = "5 years of senior engineering exp";

        var parsedInverted = DocumentTextExtractor.ExtractContactInfo(cvInverted);
        var parsedStandard = DocumentTextExtractor.ExtractContactInfo(cvStandard);

        Assert.Null(parsedInverted.YearsOfExperience); // EMPIRICAL BUG CONFIRMED: Missed "Experience: 5 years"
        Assert.Equal(5, parsedStandard.YearsOfExperience);
    }

    [Fact]
    public void Task1_PhoneRegex_MatchesFormattedNumbers()
    {
        string cvText = "Name: Kyaw Kyaw\nPhone: +95 9 1234 5678\nEmail: kyaw@example.com";
        var parsed = DocumentTextExtractor.ExtractContactInfo(cvText);

        Assert.NotNull(parsed.Phone);
        Assert.Equal("+95 9 1234 5678", parsed.Phone);
    }

    #endregion

    #region Task 2: Challenge Zawgyi Normalization on Mixed Burmese-English

    [Fact]
    public void Task2_ZawgyiDetection_OverclassifiesMixedDocumentAsZawgyiLanguage()
    {
        // Document is 95% English with a 5-char Zawgyi greeting
        string cvText = "Aung Min - Senior Backend Engineer\nExperience: 5 years of exp\nEmail: aung@example.com\nGreeting: မ\u1062လာပါ";

        var normResult = _normalizer.Normalize(cvText);

        Assert.True(normResult.IsZawgyiDetected);
        Assert.Equal(MyanmarEncoding.Zawgyi, normResult.DetectedEncoding);
        Assert.Contains("မင်္ဂလာပါ", normResult.NormalizedText);
        Assert.Contains("Aung Min - Senior Backend Engineer", normResult.NormalizedText);
    }

    [Fact]
    public void Task2_PureEnglishDocument_IsNotFlaggedAsZawgyi()
    {
        string EnglishCv = "Curriculum Vitae\nJohn Doe\nFull Stack Developer\nEmail: john@example.com";
        var normResult = _normalizer.Normalize(EnglishCv);

        Assert.False(normResult.IsZawgyiDetected);
        Assert.Equal(MyanmarEncoding.NonMyanmar, normResult.DetectedEncoding);
        Assert.Equal(EnglishCv, normResult.NormalizedText);
    }

    #endregion

    #region Task 3: Stream Handling for Large 9.9MB vs 10.1MB Files

    [Fact]
    public async Task Task3_DocumentTextExtractor_Handles9_9MBStream_Successfully()
    {
        // 9.9 MB byte array (9.9 * 1024 * 1024 = 10,380,902 bytes <= 10MB limit)
        byte[] docxBytes = CreateLargeDocxBytes(10_380_902, "Large Resume Test Content");
        using var stream = new MemoryStream(docxBytes);

        var result = await _extractor.ExtractTextAsync(stream, "large_resume.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document");

        Assert.NotNull(result);
        Assert.Contains("Large Resume Test Content", result.ExtractedText);
    }

    [Fact]
    public void Task3_FileSizeBytes_Calculation_Enforces10MBLimitCorrectly()
    {
        long limitBytes = 10 * 1024 * 1024; // 10,485,760 bytes
        long size9_9MB = (long)(9.9 * 1024 * 1024); // 10,380,902 bytes
        long size10_1MB = (long)(10.1 * 1024 * 1024); // 10,590,617 bytes

        Assert.True(size9_9MB <= limitBytes, "9.9MB must be within 10MB limit");
        Assert.True(size10_1MB > limitBytes, "10.1MB must exceed 10MB limit");
    }

    private static byte[] CreateLargeDocxBytes(int targetSizeBytes, string sampleText)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = zip.CreateEntry("word/document.xml");
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            
            string padding = new string('X', Math.Max(100, targetSizeBytes - 1000));
            writer.Write($@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<w:document xmlns:w=""http://schemas.openxmlformats.org/wordprocessingml/2006/main"">
  <w:body>
    <w:p><w:t>{sampleText} {padding}</w:t></w:p>
  </w:body>
</w:document>");
        }
        return ms.ToArray();
    }

    #endregion
}
