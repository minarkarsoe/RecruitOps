using System.Text;
using Microsoft.Extensions.DependencyInjection;
using RecruitOps.Application.Interfaces;
using RecruitOps.Infrastructure;
using RecruitOps.Infrastructure.Services.MyanmarScript;
using Xunit;

namespace RecruitOps.Api.Tests;

public class MyanmarScriptNormalizerTests
{
    private readonly IMyanmarScriptNormalizer _normalizer = new MyanmarScriptNormalizer();

    [Fact]
    public void Normalize_PureUnicodeInput_RemainsValidUnicodeNfc()
    {
        // Arrange: Pure Unicode string for "မင်္ဂလာပါ" (Mingalarpar) using standard Unicode code points
        string pureUnicode = "\u1019\u1004\u1039\u1002\u101C\u102C\u1015\u102B";

        // Act
        var result = _normalizer.Normalize(pureUnicode);

        // Assert
        Assert.False(result.IsZawgyiDetected);
        Assert.False(_normalizer.IsZawgyi(pureUnicode));
        Assert.Equal(MyanmarEncoding.Unicode, result.DetectedEncoding);
        Assert.Equal(pureUnicode, result.NormalizedText);
        Assert.True(result.NormalizedText.IsNormalized(NormalizationForm.FormC));
    }

    [Fact]
    public void Normalize_ZawgyiInput_ConvertsCorrectlyToUnicodeNfc()
    {
        // Arrange: Zawgyi encoded text using Zawgyi subjoined Ga code point \u1062
        string zawgyiInput = "မ\u1004\u1062လာပါ";
        string expectedUnicode = "\u1019\u1004\u1039\u1002\u101C\u102C\u1015\u102B";

        // Act
        var result = _normalizer.Normalize(zawgyiInput);

        // Assert
        Assert.True(result.IsZawgyiDetected);
        Assert.True(_normalizer.IsZawgyi(zawgyiInput));
        Assert.Equal(MyanmarEncoding.Zawgyi, result.DetectedEncoding);
        Assert.Equal(expectedUnicode, result.NormalizedText);
        Assert.True(result.NormalizedText.IsNormalized(NormalizationForm.FormC));
    }

    [Fact]
    public void Normalize_MixedContent_PreservesNonMyanmarTextWhileNormalizingScript()
    {
        // Arrange: Mixed English and Zawgyi text using visual E-vowel order \u1031\u1021
        string mixedInput = "Candidate Name: \u1031\u1021\u102B\u1004\u103A\u1031\u1021\u102B\u1004\u103A, Role: Dev";
        string expectedOutput = "Candidate Name: အောင်အောင်, Role: Dev";

        // Act
        var result = _normalizer.Normalize(mixedInput);

        // Assert
        Assert.True(result.IsZawgyiDetected);
        Assert.True(_normalizer.IsZawgyi(mixedInput));
        Assert.Equal(expectedOutput, result.NormalizedText);
        Assert.StartsWith("Candidate Name: ", result.NormalizedText);
        Assert.EndsWith(", Role: Dev", result.NormalizedText);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_EmptyOrNullInput_ReturnsGracefullyWithoutThrowing(string? input)
    {
        // Act
        var result = _normalizer.Normalize(input);
        bool isZawgyi = _normalizer.IsZawgyi(input);

        // Assert
        Assert.False(isZawgyi);
        Assert.False(result.IsZawgyiDetected);
        Assert.Equal(MyanmarEncoding.NonMyanmar, result.DetectedEncoding);
        Assert.Equal(input ?? string.Empty, result.NormalizedText);
    }

    [Fact]
    public void Normalize_RealWorldBurmeseSentence_ConvertsAndNormalizesCleanly()
    {
        // Arrange: Real-world Burmese sentence with digits and punctuation
        string burmeseSentence = "ကျွန်းတော်သည် Software Engineer ရာထူးဖြင့် လုပ်ငန်းအတွေ့အကြုံ ၅ နှစ်ရှိပါသည်။";

        // Act
        var result = _normalizer.Normalize(burmeseSentence);

        // Assert
        Assert.NotNull(result.NormalizedText);
        Assert.True(result.NormalizedText.IsNormalized(NormalizationForm.FormC));
        Assert.Contains("Software Engineer", result.NormalizedText);
        Assert.Contains("၅", result.NormalizedText);
    }

    [Fact]
    public void ImplicitOperator_AllowsDirectStringAssignment()
    {
        // Arrange
        string input = "\u1019\u1004\u1039\u1002\u101C\u102C\u1015\u102B";

        // Act: Implicit conversion from MyanmarScriptNormalizationResult to string
        string normalized = _normalizer.Normalize(input);

        // Assert
        Assert.Equal("\u1019\u1004\u1039\u1002\u101C\u102C\u1015\u102B", normalized);
    }

    [Fact]
    public void DependencyInjection_RegistersAsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
        services.AddInfrastructure(config);
        var provider = services.BuildServiceProvider();

        // Act
        var normalizer1 = provider.GetService<IMyanmarScriptNormalizer>();
        var normalizer2 = provider.GetService<IMyanmarScriptNormalizer>();

        // Assert
        Assert.NotNull(normalizer1);
        Assert.NotNull(normalizer2);
        Assert.Same(normalizer1, normalizer2);
    }
}
