using RecruitOps.Domain;
using RecruitOps.Domain.Enums;
using Xunit;
using static RecruitOps.Domain.PipelineAnalytics;

namespace RecruitOps.Domain.Tests;

/// <summary>ADR-0032 — the rules behind Pipeline &amp; source.</summary>
public class PipelineAnalyticsTests
{
    [Fact]
    public void The_Last_90_Days_Include_Today_And_A_Year_Starts_On_1_January()
    {
        var today = new DateOnly(2026, 10, 24);
        Assert.Equal((new DateOnly(2026, 7, 27), today), ToDate(Window.Last90Days, today));
        Assert.Equal((new DateOnly(2026, 1, 1), today), ToDate(Window.Year, today));
    }

    [Theory]
    [InlineData("90d", Window.Last90Days)]
    [InlineData("year", Window.Year)]
    public void A_Window_Is_Named_Exactly(string value, Window expected) => Assert.Equal(expected, ParseWindow(value));

    [Theory]
    [InlineData(null)]
    [InlineData("Year")]
    [InlineData("90")]
    [InlineData("0")]
    public void Anything_Else_Is_No_Window(string? value) => Assert.Null(ParseWindow(value));

    [Theory]
    [InlineData(SourceChannel.Facebook, ChannelGroup.Facebook)]
    [InlineData(SourceChannel.LinkedIn, ChannelGroup.LinkedIn)]
    [InlineData(SourceChannel.Direct, ChannelGroup.CareerPage)]
    [InlineData(SourceChannel.ManualUpload, ChannelGroup.ManualUpload)]
    [InlineData(SourceChannel.Telegram, ChannelGroup.Other)]
    [InlineData(SourceChannel.Viber, ChannelGroup.Other)]
    [InlineData(SourceChannel.Referral, ChannelGroup.Other)]
    [InlineData(SourceChannel.ExcelImport, ChannelGroup.Other)]
    public void The_Spec_Names_Four_Channels_And_The_Rest_Are_Other(SourceChannel channel, ChannelGroup group) =>
        Assert.Equal(group, GroupOf(channel));

    [Fact]
    public void A_Group_Is_Named_Exactly()
    {
        Assert.Equal(ChannelGroup.CareerPage, ParseGroup("CareerPage"));
        Assert.Null(ParseGroup("careerpage"));
        Assert.Null(ParseGroup("2"));
        Assert.Null(ParseGroup(null));
    }

    [Theory]
    [InlineData(new[] { PipelineStatus.Sourced }, Band.Sourcing)]
    [InlineData(new[] { PipelineStatus.Applied, PipelineStatus.Shortlisted }, Band.Screened)]
    [InlineData(new[] { PipelineStatus.Applied, PipelineStatus.Interview }, Band.Interview)]           // skipped screening
    [InlineData(new[] { PipelineStatus.Applied, PipelineStatus.Interview, PipelineStatus.Rejected }, Band.Interview)]
    [InlineData(new[] { PipelineStatus.Applied, PipelineStatus.Offer, PipelineStatus.Screening }, Band.Offered)] // moved back
    [InlineData(new[] { PipelineStatus.Hired }, Band.Offered)]
    [InlineData(new[] { PipelineStatus.Rejected }, Band.Sourcing)]
    [InlineData(new PipelineStatus[0], Band.Sourcing)]
    public void An_Application_Counts_In_The_Furthest_Band_It_Reached(PipelineStatus[] statuses, Band expected) =>
        Assert.Equal(expected, Reached(statuses));
}
