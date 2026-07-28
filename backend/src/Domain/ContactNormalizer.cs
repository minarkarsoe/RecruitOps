using System.Text.RegularExpressions;

namespace RecruitOps.Domain;

/// <summary>Normalises the two fields duplicate detection keys on (Module 2.7).
///
/// <para>This lives in Domain rather than in a service because both the public application
/// path and any future CV-import path must produce byte-identical values — if they normalise
/// differently, the same person imported twice will not be recognised as a duplicate, and
/// nothing will report the failure.</para>
/// </summary>
public static class ContactNormalizer
{
    private static readonly Regex NonDigits = new(@"\D", RegexOptions.Compiled);

    /// <summary>Lower-cased and trimmed. Not case-folded per-locale: email addresses are
    /// ASCII-cased by convention and invariant lowering is what the database index expects.</summary>
    public static string? Email(string? email) =>
        string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();

    /// <summary>Digits only, with a leading Myanmar country code reduced to the national
    /// form. "+95 9 765 432 100", "0095 9765432100" and "09765432100" are the same phone,
    /// and applicants type all three — comparing the raw strings would miss every duplicate
    /// that formatted their number differently.
    ///
    /// <para>Deliberately naive about other countries: this product's market is Myanmar
    /// (ADR-0009). A general E.164 parser is the right answer when that stops being true,
    /// and this method is the single place that has to change.</para></summary>
    public static string? Phone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;

        var digits = NonDigits.Replace(phone, string.Empty);
        if (digits.Length == 0) return null;

        // 0095XXXXXXXXX / 95XXXXXXXXX → 0XXXXXXXXX
        if (digits.StartsWith("0095", StringComparison.Ordinal)) digits = "0" + digits[4..];
        else if (digits.StartsWith("95", StringComparison.Ordinal) && digits.Length >= 10) digits = "0" + digits[2..];

        return digits;
    }
}
