using System.Text;
using RecruitOps.Application.Interfaces;
using RecruitOps.Infrastructure.Services.MyanmarScript;
using Xunit;

namespace RecruitOps.Api.Tests;

public class MyanmarScriptNormalizerChallengerTests
{
    private readonly IMyanmarScriptNormalizer _normalizer = new MyanmarScriptNormalizer();

    [Theory]
    [InlineData("\u101E\u1005\u103A\u101E\u102C\u1038", "သစ်သား")] // Wood
    [InlineData("\u1021\u101E\u1005\u103A\u1015\u103B\u1010\u103C\u102C\u1004\u103B\u1038", "အသစ်ပြောင်း")] // Newly changed
    [InlineData("\u1019\u1004\u103A\u1019\u1002\u1039\u1002\u101C\u102C", "မင်မင်္ဂလာ")] // Min Mingalar
    [InlineData("\u1005\u1005\u103A\u1000\u102D\u102F\u1004\u103A\u1038", "စစ်ကိုင်း")] // Sagaing
    public void Normalize_ValidUnicodeWithAsatConsonantSequence_ShouldNotBeDetectedAsZawgyiOrCorrupted(string validUnicode, string label)
    {
        var result = _normalizer.Normalize(validUnicode);

        // Valid Unicode should NOT be flagged as Zawgyi
        Assert.False(result.IsZawgyiDetected, $"[Failed for '{label}'] Expected false for valid Unicode, but got IsZawgyiDetected=true with confidence {result.ConfidenceScore}");
        Assert.Equal(MyanmarEncoding.Unicode, result.DetectedEncoding);
        Assert.Equal(validUnicode, result.NormalizedText);
    }

    [Fact]
    public void Normalize_NfdUnicodeInput_NormalizesToNfcWithoutZawgyiFalsePositive()
    {
        string unicodeNfc = "\u1019\u1004\u1039\u1002\u101C\u102C\u1015\u102B"; // မင်္ဂလာပါ
        string unicodeNfd = unicodeNfc.Normalize(NormalizationForm.FormD);

        var result = _normalizer.Normalize(unicodeNfd);

        Assert.False(result.IsZawgyiDetected);
        Assert.Equal(MyanmarEncoding.Unicode, result.DetectedEncoding);
        Assert.True(result.NormalizedText.IsNormalized(NormalizationForm.FormC));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void Normalize_NullOrWhitespace_ReturnsNonMyanmarResult(string? input)
    {
        var result = _normalizer.Normalize(input);

        Assert.False(result.IsZawgyiDetected);
        Assert.Equal(MyanmarEncoding.NonMyanmar, result.DetectedEncoding);
        Assert.Equal(input ?? string.Empty, result.NormalizedText);
    }

    [Fact]
    public void Normalize_MixedEnglishAndZawgyi_ConvertsZawgyiPartAndPreservesEnglish()
    {
        // English + Zawgyi ("မ\u1062လာပါ")
        string mixedInput = "Applicant: John Doe, Greetings: မ\u1062လာပါ, Status: Active";
        string expectedUnicode = "Applicant: John Doe, Greetings: မင်္ဂလာပါ, Status: Active";

        var result = _normalizer.Normalize(mixedInput);

        Assert.True(result.IsZawgyiDetected);
        Assert.Equal(expectedUnicode, result.NormalizedText);
    }
}
