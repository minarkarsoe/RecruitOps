namespace RecruitOps.Domain;

/// <summary>The executive dashboard's periods (Module 5.1, ADR-0031 §2): a month, quarter or year to date,
/// on the company's calendar, compared with the whole previous one.</summary>
public static class ReportingPeriod
{
    public enum Kind { Month, Quarter, Year }

    /// <summary>A kind named in a query string, by its exact name — never <c>Enum.TryParse</c>, which also
    /// takes numbers and comma-joined names. Null for anything else.</summary>
    public static Kind? Parse(string? value) => value switch
    {
        nameof(Kind.Month) => Kind.Month,
        nameof(Kind.Quarter) => Kind.Quarter,
        nameof(Kind.Year) => Kind.Year,
        _ => null,
    };

    /// <summary>The period <paramref name="today"/> falls in, from its first day to today.</summary>
    public static (DateOnly From, DateOnly To) ToDate(Kind kind, DateOnly today) => (Start(kind, today), today);

    /// <summary>The whole period before the one <paramref name="today"/> falls in.</summary>
    public static (DateOnly From, DateOnly To) Previous(Kind kind, DateOnly today)
    {
        var start = Start(kind, today);
        var previousStart = kind switch
        {
            Kind.Month => start.AddMonths(-1),
            Kind.Quarter => start.AddMonths(-3),
            _ => start.AddYears(-1),
        };
        return (previousStart, start.AddDays(-1));
    }

    private static DateOnly Start(Kind kind, DateOnly today) => kind switch
    {
        Kind.Month => new DateOnly(today.Year, today.Month, 1),
        Kind.Quarter => new DateOnly(today.Year, (today.Month - 1) / 3 * 3 + 1, 1),
        _ => new DateOnly(today.Year, 1, 1),
    };
}
