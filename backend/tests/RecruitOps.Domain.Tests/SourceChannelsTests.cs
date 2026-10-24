using RecruitOps.Domain;
using RecruitOps.Domain.Enums;
using Xunit;

namespace RecruitOps.Domain.Tests;

/// <summary>ADR-0032 §1 — what an anonymous applicant may claim as their channel.</summary>
public class SourceChannelsTests
{
    [Theory]
    [InlineData("Facebook", SourceChannel.Facebook)]
    [InlineData("LinkedIn", SourceChannel.LinkedIn)]
    [InlineData("Telegram", SourceChannel.Telegram)]
    [InlineData("Viber", SourceChannel.Viber)]
    [InlineData("Referral", SourceChannel.Referral)]
    public void A_Link_Channel_Is_Taken_By_Its_Exact_Name(string value, SourceChannel expected) =>
        Assert.Equal(expected, SourceChannels.FromPublic(value));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Direct")]
    [InlineData("ManualUpload")]
    [InlineData("ExcelImport")]
    [InlineData("LINKEDIN")]
    [InlineData(" Facebook")]
    [InlineData("6")]
    [InlineData("Facebook,Viber")]
    public void Anything_Else_Is_The_Career_Page(string? value) =>
        Assert.Equal(SourceChannel.Direct, SourceChannels.FromPublic(value));
}
