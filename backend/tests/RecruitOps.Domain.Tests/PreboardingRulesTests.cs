using RecruitOps.Domain;
using RecruitOps.Domain.Enums;
using Xunit;

namespace RecruitOps.Domain.Tests;

/// <summary>Pre-boarding's rules (ADR-0030 §5b) that need no database.</summary>
public class PreboardingRulesTests
{
    [Theory]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0 }, PreboardingDocumentKind.NrcFront, Preboarding.Jpeg)]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, PreboardingDocumentKind.Photo, Preboarding.Png)]
    [InlineData(new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31 }, PreboardingDocumentKind.Education, Preboarding.Pdf)]
    [InlineData(new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31 }, PreboardingDocumentKind.Photo, null)] // a staff card needs a picture
    [InlineData(new byte[] { 0x3C, 0x73, 0x76, 0x67 }, PreboardingDocumentKind.NrcFront, null)] // "<svg"
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, PreboardingDocumentKind.NrcFront, null)] // half a PNG signature
    [InlineData(new byte[] { 0xFF, 0xD8 }, PreboardingDocumentKind.NrcFront, null)]
    [InlineData(new byte[0], PreboardingDocumentKind.NrcFront, null)]
    public void A_File_Is_What_Its_First_Bytes_Say(byte[] head, PreboardingDocumentKind kind, string? expected) =>
        Assert.Equal(expected, Preboarding.Sniff(head, kind));

    [Theory]
    [InlineData("0012 3456 7890 4471", "0012345678904471")]
    [InlineData("၀၀၁၂-၃၄၅၆", "00123456")] // a Myanmar keyboard's digits
    [InlineData("12345", null)] // too short to be an account
    [InlineData("1234567890123456789012345678901", null)] // 31 digits
    [InlineData("12-AB-34", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void An_Account_Number_Is_6_To_30_Digits_In_Any_Script(string? typed, string? expected) =>
        Assert.Equal(expected, Preboarding.NormalizeAccountNumber(typed));

    [Fact]
    public void An_Account_Number_Shows_Only_Its_Last_Four_Digits() =>
        Assert.Equal("•••• 4471", Preboarding.MaskAccountNumber("0012345678904471"));

    [Theory]
    [InlineData("C:\\Users\\me\\nrc front.jpg", "nrc front.jpg")]
    [InlineData("../../etc/passwd", "passwd")]
    [InlineData("a\r\nb.png", "a b.png")]
    [InlineData("\u202Egpj.exe", "gpj.exe")] // the right-to-left override that disguises an extension
    [InlineData("", "file")]
    [InlineData(null, "file")]
    public void A_File_Name_Is_For_Display_On_One_Line_Without_A_Folder(string? name, string expected) =>
        Assert.Equal(expected, Preboarding.CleanFileName(name));

    [Fact]
    public void A_Long_File_Name_Is_Cut_To_200_Characters() =>
        Assert.Equal(Preboarding.MaxFileNameLength, Preboarding.CleanFileName(new string('a', 500) + ".jpg").Length);

    [Fact]
    public void A_Long_File_Name_Is_Never_Cut_Between_The_Halves_Of_A_Character()
    {
        // An emoji straddling the 200th position: cutting there left half a character, which the database
        // refuses after the file is already in storage (security review, L2).
        var name = Preboarding.CleanFileName(new string('a', 199) + "😀" + "tail.jpg");
        Assert.Equal(199, name.Length);
        Assert.False(char.IsSurrogate(name[^1]));
    }

    [Theory]
    [InlineData("  Please retake it.  ", "Please retake it.")]
    [InlineData("ဓာတ်ပုံ\u200Bမှုန်ဝါး\r\nRetake it.", "ဓာတ်ပုံ\u200Bမှုန်ဝါး\nRetake it.")] // Burmese word breaks kept
    [InlineData("\u202Eti ekater", "ti ekater")]
    [InlineData("\u200B\u200B", null)]
    [InlineData(" \n ", null)]
    [InlineData(null, null)]
    public void A_Message_To_The_Candidate_Keeps_Its_Lines_And_Must_Say_Something(string? typed, string? expected) =>
        Assert.Equal(expected, Preboarding.CleanMessage(typed));

    [Theory]
    [InlineData("NrcFront", PreboardingDocumentKind.NrcFront)]
    [InlineData("Photo", PreboardingDocumentKind.Photo)]
    [InlineData("0", null)] // Enum.TryParse would take a number
    [InlineData("NrcFront,NrcBack", null)] // and a list
    [InlineData("nrcfront", null)]
    [InlineData(null, null)]
    public void A_Document_Is_Named_Exactly(string? value, PreboardingDocumentKind? expected) =>
        Assert.Equal(expected, Preboarding.ParseKind(value));

    [Fact]
    public void The_Default_Requires_Nrc_Bank_And_Education_And_Nrc_Is_Two_Documents()
    {
        var required = Preboarding.DefaultRequired;
        Assert.True(Preboarding.IsRequired(required, PreboardingDocumentKind.NrcFront));
        Assert.True(Preboarding.IsRequired(required, PreboardingDocumentKind.NrcBack));
        Assert.True(Preboarding.IsRequired(required, PreboardingDocumentKind.BankAccount));
        Assert.True(Preboarding.IsRequired(required, PreboardingDocumentKind.Education));
        Assert.False(Preboarding.IsRequired(required, PreboardingDocumentKind.Photo));
        Assert.Equal(7, (int)required); // the migration's default for existing companies
        Assert.Equal(5, Preboarding.MaxFiles(PreboardingDocumentKind.Education));
        Assert.Equal(1, Preboarding.MaxFiles(PreboardingDocumentKind.NrcFront));
    }

    [Fact]
    public void Documents_Close_At_The_Joining_Date_Or_90_Days_After_Accepting_Whichever_Is_First()
    {
        var accepted = new DateOnly(2026, 8, 20);
        Assert.Equal(new DateOnly(2026, 10, 1), Preboarding.DocumentsOpenUntil(new DateOnly(2026, 10, 1), accepted));
        // A joining date a year out — or a typo — does not keep identity documents open that long.
        Assert.Equal(new DateOnly(2026, 11, 18), Preboarding.DocumentsOpenUntil(new DateOnly(2027, 8, 20), accepted));
    }

    [Fact]
    public void An_Accepted_Link_Lasts_To_The_Later_Of_Reply_By_And_The_Documents_Last_Day()
    {
        var replyBy = new DateOnly(2026, 8, 25);
        var documentsUntil = new DateOnly(2026, 10, 1);
        Assert.Equal(documentsUntil, Preboarding.LinkOpenUntil(replyBy, documentsUntil));
        Assert.Equal(new DateOnly(2026, 10, 5), Preboarding.LinkOpenUntil(new DateOnly(2026, 10, 5), documentsUntil));
    }

    [Fact]
    public void Documents_Can_Be_Sent_To_The_End_Of_The_Joining_Date_In_The_Companys_Zone()
    {
        var joining = new DateOnly(2026, 10, 1);
        // 23:59 on 1 October in Yangon (UTC+06:30) is 17:29 UTC: still open.
        Assert.True(Preboarding.IsWindowOpen(joining, new DateTimeOffset(2026, 10, 1, 17, 29, 0, TimeSpan.Zero), "Asia/Yangon"));
        // 00:01 on 2 October in Yangon is still 1 October in UTC: closed for the company, open in UTC.
        var justAfter = new DateTimeOffset(2026, 10, 1, 17, 31, 0, TimeSpan.Zero);
        Assert.False(Preboarding.IsWindowOpen(joining, justAfter, "Asia/Yangon"));
        Assert.True(Preboarding.IsWindowOpen(joining, justAfter, null));
    }
}
