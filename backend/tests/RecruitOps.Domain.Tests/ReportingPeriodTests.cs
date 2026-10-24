using RecruitOps.Domain;
using Xunit;

namespace RecruitOps.Domain.Tests;

/// <summary>The executive dashboard's periods (ADR-0031 §2).</summary>
public class ReportingPeriodTests
{
    [Theory]
    [InlineData("2026-08-18", "Month", "2026-08-01", "2026-07-01", "2026-07-31")]
    [InlineData("2026-08-18", "Quarter", "2026-07-01", "2026-04-01", "2026-06-30")]
    [InlineData("2026-08-18", "Year", "2026-01-01", "2025-01-01", "2025-12-31")]
    [InlineData("2026-01-05", "Quarter", "2026-01-01", "2025-10-01", "2025-12-31")]
    [InlineData("2026-03-31", "Month", "2026-03-01", "2026-02-01", "2026-02-28")]
    [InlineData("2028-03-01", "Month", "2028-03-01", "2028-02-01", "2028-02-29")]
    public void A_Period_Runs_To_Today_And_The_Previous_One_Is_Whole(string today, string kind, string from, string prevFrom, string prevTo)
    {
        var day = DateOnly.Parse(today);
        var k = ReportingPeriod.Parse(kind)!.Value;
        Assert.Equal((DateOnly.Parse(from), day), ReportingPeriod.ToDate(k, day));
        Assert.Equal((DateOnly.Parse(prevFrom), DateOnly.Parse(prevTo)), ReportingPeriod.Previous(k, day));
    }

    [Theory]
    [InlineData("Week")]
    [InlineData("0")]
    [InlineData("quarter")]
    [InlineData("Month, Year")]
    [InlineData(null)]
    public void A_Period_Is_Named_Exactly(string? value) => Assert.Null(ReportingPeriod.Parse(value));
}
