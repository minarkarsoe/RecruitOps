using System.Globalization;
using System.Text.RegularExpressions;

namespace RecruitOps.Domain;

/// <summary>Offer-letter merge fields (Module 4.2). A letter is stored with its <c>{{Field}}</c>
/// placeholders and rendered from the offer's current terms at preview and at send, so editing the
/// salary never leaves a stale figure in the text.
///
/// <para>Rendering produces plain text. Whatever displays it escapes it — a candidate's name is
/// text, never markup.</para></summary>
public static class OfferLetter
{
    /// <summary>Used until a company adds a template of its own.</summary>
    public const string DefaultTemplateName = "Standard offer letter";

    public const string DefaultBody =
        "Dear {{CandidateName}},\n\n" +
        "We are pleased to offer you the position of {{Position}} in the {{Department}} department at {{CompanyName}}.\n\n" +
        "Your base salary will be MMK {{BaseSalary}} a month, with allowances of MMK {{Allowances}} " +
        "(MMK {{TotalPackage}} a month in total). Your joining date is {{JoiningDate}}, and your employment " +
        "begins with a probation period of {{ProbationMonths}} months.\n\n" +
        "This offer is open until {{ExpiresOn}}. To accept it, please sign through the secure link in the " +
        "email that came with this letter.\n\n" +
        "We look forward to welcoming you.";

    /// <summary>The fields a template may use, for the editor's hint and for validation.</summary>
    public static IReadOnlyList<string> Fields { get; } = new[]
    {
        "CandidateName", "Position", "Department", "CompanyName", "BaseSalary", "Allowances",
        "TotalPackage", "JoiningDate", "ProbationMonths", "ExpiresOn",
    };

    private static readonly Regex Placeholder = new(
        @"\{\{\s*([A-Za-z]+)\s*\}\}", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200));

    public sealed record Terms(
        string CandidateName, string Position, string Department, string CompanyName,
        decimal BaseSalary, decimal Allowances, DateOnly JoiningDate, int ProbationMonths, DateOnly ExpiresOn);

    /// <summary>Fills every known placeholder; an unknown one is left as typed, so a mistyped field
    /// is visible in the preview instead of silently becoming blank.</summary>
    public static string Render(string body, Terms t)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // One line each: a name comes from an anonymous form, and a line break in it would add
            // paragraphs to the contract text the candidate signs.
            ["CandidateName"] = OneLine(t.CandidateName),
            ["Position"] = OneLine(t.Position),
            ["Department"] = OneLine(t.Department),
            ["CompanyName"] = OneLine(t.CompanyName),
            ["BaseSalary"] = Money(t.BaseSalary),
            ["Allowances"] = Money(t.Allowances),
            ["TotalPackage"] = Money(t.BaseSalary + t.Allowances),
            ["JoiningDate"] = Date(t.JoiningDate),
            ["ProbationMonths"] = t.ProbationMonths.ToString(CultureInfo.InvariantCulture),
            ["ExpiresOn"] = Date(t.ExpiresOn),
        };
        return Placeholder.Replace(body, m => values.TryGetValue(m.Groups[1].Value, out var v) ? v : m.Value);
    }

    private static readonly Regex Breaks = new(@"[\s\p{Cc}\p{Zl}\p{Zp}]+", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200));

    /// <summary>The text on one line: every run of whitespace, control or separator characters becomes one
    /// space. Shared with the typed signature, which must not carry line breaks into the record either.</summary>
    public static string OneLine(string text) => Breaks.Replace(text, " ").Trim();

    private static string Money(decimal amount) => amount.ToString("#,##0.##", CultureInfo.InvariantCulture);

    private static string Date(DateOnly d) => d.ToString("d MMMM yyyy", CultureInfo.InvariantCulture);
}
