using Xunit;

namespace RecruitOps.Domain.Tests;

/// <summary>Module 2.7 — the values duplicate detection keys on.
///
/// <para>A miss here is silent by construction: two records for one person, and nothing reports
/// it. The Burmese-digit cases were found 2026-10-17 by applying twice to a running install with the
/// same number, once as a Myanmar keyboard types it — two candidates came back.</para>
/// </summary>
public class ContactNormalizerTests
{
    [Theory]
    [InlineData("09765432100", "09765432100")]
    [InlineData("09 765 432 100", "09765432100")]
    [InlineData("+95 9 765 432 100", "09765432100")]
    [InlineData("0095 9765432100", "09765432100")]
    [InlineData("(09) 765-432-100", "09765432100")]
    public void Ascii_Formats_Of_One_Number_Normalise_Alike(string typed, string expected) =>
        Assert.Equal(expected, ContactNormalizer.Phone(typed));

    [Theory]
    [InlineData("၀၉၇၆၅၄၃၂၁၀၀")]             // Burmese digits, as a Myanmar keyboard types them
    [InlineData("၀၉ ၇၆၅ ၄၃၂ ၁၀၀")]
    [InlineData("+၉၅ ၉ ၇၆၅ ၄၃၂ ၁၀၀")]       // the country code in Burmese digits too
    [InlineData("၀၀၉၅ ၉၇၆၅၄၃၂၁၀၀")]
    [InlineData("09 ၇၆၅ ၄၃၂ ၁၀၀")]          // mixed: some apps switch layout mid-number
    public void Burmese_Digits_Are_The_Same_Number_As_Ascii(string typed) =>
        Assert.Equal("09765432100", ContactNormalizer.Phone(typed));

    [Fact]
    public void The_Result_Is_Always_Ascii_So_Search_And_The_Index_Agree() =>
        Assert.Matches("^[0-9]+$", ContactNormalizer.Phone("၀၉ ၇၆၅ ၄၃၂ ၁၀၀")!);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("call me")]
    public void No_Digits_Means_No_Phone(string? typed) =>
        Assert.Null(ContactNormalizer.Phone(typed));

    [Theory]
    [InlineData(" Aung.Aung@Example.COM ", "aung.aung@example.com")]
    [InlineData("", null)]
    [InlineData("  ", null)]
    [InlineData(null, null)]
    public void Email_Is_Trimmed_And_Lower_Cased(string? typed, string? expected) =>
        Assert.Equal(expected, ContactNormalizer.Email(typed));
}
