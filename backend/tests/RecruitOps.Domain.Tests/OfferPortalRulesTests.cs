using RecruitOps.Domain;
using Xunit;

namespace RecruitOps.Domain.Tests;

/// <summary>The candidate link's rules (ADR-0030 §5a) that need no database.</summary>
public class OfferPortalRulesTests
{
    [Theory]
    [InlineData("123456", "123456")]
    [InlineData(" 123 456 ", "123456")]
    [InlineData("123-456", "123456")]
    [InlineData("၁၂၃၄၅၆", "123456")] // a Myanmar keyboard's digits
    [InlineData("12345", null)]
    [InlineData("1234567", null)]
    [InlineData("12345a", null)]
    [InlineData("", null)]
    public void A_Code_Is_Six_Digits_In_Any_Script(string typed, string? expected) =>
        Assert.Equal(expected, OfferPortal.NormalizeCode(typed));

    [Fact]
    public void A_New_Code_Is_Six_Ascii_Digits_And_A_Token_Is_43_Url_Safe_Characters()
    {
        for (var i = 0; i < 200; i++)
        {
            Assert.Matches("^[0-9]{6}$", OfferPortal.NewCode());
            var token = OfferPortal.NewToken();
            Assert.True(OfferPortal.LooksLikeToken(token), token);
        }
        Assert.NotEqual(OfferPortal.NewToken(), OfferPortal.NewToken());
        Assert.False(OfferPortal.LooksLikeToken(null));
        Assert.False(OfferPortal.LooksLikeToken(new string('a', 42)));
        Assert.False(OfferPortal.LooksLikeToken(new string('a', 42) + "/"));
    }

    [Fact]
    public void A_Code_Hash_Is_Keyed_And_Salted_By_Its_Row()
    {
        var key = new byte[32];
        var otherKey = Enumerable.Repeat((byte)1, 32).ToArray();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        Assert.Equal(OfferPortal.CodeHash(key, a, "123456"), OfferPortal.CodeHash(key, a, "123456"));
        Assert.NotEqual(OfferPortal.CodeHash(key, a, "123456"), OfferPortal.CodeHash(key, b, "123456"));
        // Without the key, the hash cannot be recomputed from the row — or reversed by trying a million codes.
        Assert.NotEqual(OfferPortal.CodeHash(key, a, "123456"), OfferPortal.CodeHash(otherKey, a, "123456"));
        Assert.NotEqual(OfferPortal.Sha256Hex($"{a}:123456"), OfferPortal.CodeHash(key, a, "123456"));
        Assert.Matches("^[0-9a-f]{64}$", OfferPortal.CodeHash(key, a, "123456"));
    }

    [Theory]
    [InlineData("kyaw@gmail.com", "k•••••@gmail.com")]
    [InlineData("a@b.mm", "a•••••@b.mm")]
    [InlineData("not-an-address", "•••••")]
    public void A_Masked_Address_Keeps_Only_The_First_Letter_And_The_Domain(string email, string expected) =>
        Assert.Equal(expected, OfferPortal.MaskEmail(email));

    [Theory]
    [InlineData("  Aung\nKyaw\t Moe ", "Aung Kyaw Moe")]
    [InlineData("ဒေါ်ခင်မာလာ", "ဒေါ်ခင်မာလာ")]
    [InlineData("\u0007", null)]
    [InlineData("", null)]
    public void A_Signature_Is_One_Line(string typed, string? expected) =>
        Assert.Equal(expected, OfferPortal.CleanSignatureName(typed));

    [Fact]
    public void A_Signature_Longer_Than_200_Characters_Is_Refused() =>
        Assert.Null(OfferPortal.CleanSignatureName(new string('a', 201)));

    [Fact]
    public void Expiry_Is_Measured_In_The_Companys_Time_Zone()
    {
        var expiresOn = new DateOnly(2026, 10, 30);
        // 18:00 UTC on the 30th is 00:30 on the 31st in Yangon (UTC+6:30).
        var evening = new DateTimeOffset(2026, 10, 30, 18, 0, 0, TimeSpan.Zero);
        Assert.False(OfferPortal.IsPastExpiry(expiresOn, evening, null));
        Assert.True(OfferPortal.IsPastExpiry(expiresOn, evening, "Asia/Yangon"));
        // An unknown zone falls back to UTC rather than failing.
        Assert.False(OfferPortal.IsPastExpiry(expiresOn, evening, "Mars/Olympus_Mons"));
    }
}
