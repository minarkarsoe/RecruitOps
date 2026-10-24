using RecruitOps.Domain.Enums;

namespace RecruitOps.Domain;

/// <summary>The rules behind Module 5.2's charts (ADR-0032): the period, the channel groups and the funnel's
/// four bands. Kept here, apart from the queries, so each rule is pinned by itself.</summary>
public static class PipelineAnalytics
{
    public enum Window { Last90Days, Year }

    /// <summary><c>90d</c> or <c>year</c>, exactly. Null for anything else.</summary>
    public static Window? ParseWindow(string? value) => value switch
    {
        "90d" => Window.Last90Days,
        "year" => Window.Year,
        _ => null,
    };

    /// <summary>The days a window covers, ending today on the company's calendar: the last 90 days include
    /// today, and a year runs from 1 January.</summary>
    public static (DateOnly From, DateOnly To) ToDate(Window window, DateOnly today) => window switch
    {
        Window.Year => (new DateOnly(today.Year, 1, 1), today),
        _ => (today.AddDays(-89), today),
    };

    /// <summary>The channel groups the charts draw, in their fixed colour order (ADR-0032 §3).</summary>
    public enum ChannelGroup { Facebook, LinkedIn, CareerPage, ManualUpload, Other }

    public static ChannelGroup GroupOf(SourceChannel channel) => channel switch
    {
        SourceChannel.Facebook => ChannelGroup.Facebook,
        SourceChannel.LinkedIn => ChannelGroup.LinkedIn,
        SourceChannel.Direct => ChannelGroup.CareerPage,
        SourceChannel.ManualUpload => ChannelGroup.ManualUpload,
        _ => ChannelGroup.Other,
    };

    /// <summary>A group named in a query string, by its exact name. Null for anything else.</summary>
    public static ChannelGroup? ParseGroup(string? value) =>
        Enum.GetValues<ChannelGroup>().Select(g => (ChannelGroup?)g).FirstOrDefault(g => g.ToString() == value);

    /// <summary>The funnel's bands (ADR-0032 §4), in order.</summary>
    public enum Band { Sourcing, Screened, Interview, Offered }

    /// <summary>The band a status belongs to — none for Rejected, which is an exit from every stage.</summary>
    public static Band? BandOf(PipelineStatus status) => status switch
    {
        PipelineStatus.Sourced or PipelineStatus.Applied => Band.Sourcing,
        PipelineStatus.Screening or PipelineStatus.Shortlisted => Band.Screened,
        PipelineStatus.Interview => Band.Interview,
        PipelineStatus.Offer or PipelineStatus.Hired => Band.Offered,
        _ => null,
    };

    /// <summary>The furthest band an application reached, from every status it has held. Every application
    /// reached Sourcing: it is in the pipeline.</summary>
    public static Band Reached(IEnumerable<PipelineStatus> statuses) =>
        statuses.Select(BandOf).Where(b => b is not null).Select(b => b!.Value) // non-null: filtered above
            .DefaultIfEmpty(Band.Sourcing).Max();
}
