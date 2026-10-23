using System.Globalization;
using System.Text;
using RecruitOps.Domain.Enums;

namespace RecruitOps.Domain;

/// <summary>The rules of pre-boarding (Module 4.3, ADR-0030 §5b) in one place, so the candidate's side,
/// the recruiter's side, the tests and the numbers in the ADR cannot drift apart.</summary>
public static class Preboarding
{
    /// <summary>What a company requires until it says otherwise — as the portal was drawn: the photo is
    /// optional ("you can do this on day one instead").</summary>
    public const PreboardingItems DefaultRequired =
        PreboardingItems.Nrc | PreboardingItems.BankAccount | PreboardingItems.Education;

    public const PreboardingItems AllItems =
        PreboardingItems.Nrc | PreboardingItems.BankAccount | PreboardingItems.Education | PreboardingItems.Photo;

    /// <summary>Every document, in the order both screens list them.</summary>
    public static IReadOnlyList<PreboardingDocumentKind> Kinds { get; } = new[]
    {
        PreboardingDocumentKind.NrcFront, PreboardingDocumentKind.NrcBack, PreboardingDocumentKind.BankAccount,
        PreboardingDocumentKind.Education, PreboardingDocumentKind.Photo,
    };

    public const long MaxFileBytes = 10 * 1024 * 1024;

    public const int MaxEducationFiles = 5;

    public const int MaxReturnMessageLength = 1000;

    public const int MaxFileNameLength = 200;

    public const int MaxBankNameLength = 100;

    public const int MaxAccountNameLength = 200;

    /// <summary>The shortest gap between two reminders.</summary>
    public static readonly TimeSpan ReminderInterval = TimeSpan.FromHours(24);

    public static PreboardingItems ItemOf(PreboardingDocumentKind kind) => kind switch
    {
        PreboardingDocumentKind.NrcFront or PreboardingDocumentKind.NrcBack => PreboardingItems.Nrc,
        PreboardingDocumentKind.BankAccount => PreboardingItems.BankAccount,
        PreboardingDocumentKind.Education => PreboardingItems.Education,
        _ => PreboardingItems.Photo,
    };

    public static bool IsRequired(PreboardingItems required, PreboardingDocumentKind kind) =>
        (required & ItemOf(kind)) != 0;

    /// <summary>A kind named in a URL, by its exact name — never <c>Enum.TryParse</c>, which also takes
    /// numbers and comma-joined names.</summary>
    public static PreboardingDocumentKind? ParseKind(string? value) =>
        Kinds.Cast<PreboardingDocumentKind?>().FirstOrDefault(k => string.Equals(k.ToString(), value, StringComparison.Ordinal));

    public static int MaxFiles(PreboardingDocumentKind kind) =>
        kind == PreboardingDocumentKind.Education ? MaxEducationFiles : 1;

    public static string Label(PreboardingDocumentKind kind) => kind switch
    {
        PreboardingDocumentKind.NrcFront => "NRC — front",
        PreboardingDocumentKind.NrcBack => "NRC — back",
        PreboardingDocumentKind.BankAccount => "Bank account details",
        PreboardingDocumentKind.Education => "Education certificates",
        _ => "Photo for the staff card",
    };

    public const string Jpeg = "image/jpeg";
    public const string Png = "image/png";
    public const string Pdf = "application/pdf";

    /// <summary>The type a file really is, from its first bytes — JPEG, PNG or PDF, and for the staff-card
    /// photo JPEG or PNG only — or null. What the browser declared and what the name ends in are never
    /// asked: both are the uploader's to choose.</summary>
    public static string? Sniff(ReadOnlySpan<byte> head, PreboardingDocumentKind kind)
    {
        string? type =
            head.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }) ? Jpeg
            : head.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }) ? Png
            : head.StartsWith("%PDF-"u8) ? Pdf
            : null;
        return kind == PreboardingDocumentKind.Photo && type == Pdf ? null : type;
    }

    public static string Extension(string contentType) => contentType switch
    {
        Jpeg => ".jpg",
        Png => ".png",
        _ => ".pdf",
    };

    /// <summary>The name on the candidate's device, for display: no folder, on one line, no control
    /// characters, at most <see cref="MaxFileNameLength"/> characters. "file" when nothing is left.</summary>
    public static string CleanFileName(string? name)
    {
        var bare = (name ?? string.Empty).Replace('\\', '/');
        bare = bare[(bare.LastIndexOf('/') + 1)..];
        var clean = new string(OfferLetter.OneLine(bare).Where(c => !char.IsControl(c) && CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.Format).ToArray()).Trim();
        if (clean.Length > MaxFileNameLength) clean = clean[..MaxFileNameLength];
        return clean.Length == 0 ? "file" : clean;
    }

    /// <summary>An account number as typed, in ASCII digits whatever the script (a Myanmar keyboard types
    /// ၀–၉), spaces and dashes ignored; null unless 6–30 digits are left.</summary>
    public static string? NormalizeAccountNumber(string? typed)
    {
        if (string.IsNullOrWhiteSpace(typed)) return null;
        var digits = new StringBuilder();
        foreach (var c in typed)
        {
            var value = CharUnicodeInfo.GetDecimalDigitValue(c);
            if (value >= 0) digits.Append((char)('0' + value));
            else if (!char.IsWhiteSpace(c) && c != '-') return null;
            if (digits.Length > 30) return null;
        }
        return digits.Length >= 6 ? digits.ToString() : null;
    }

    /// <summary><c>•••• 4471</c>.</summary>
    public static string MaskAccountNumber(string number) =>
        number.Length <= 4 ? "••••" : $"•••• {number[^4..]}";

    /// <summary>A typed bank detail on one line, or null when empty or longer than <paramref name="max"/>.</summary>
    public static string? CleanText(string? typed, int max)
    {
        if (typed is null) return null;
        var text = OfferLetter.OneLine(typed);
        return text.Length is > 0 && text.Length <= max ? text : null;
    }

    /// <summary>A recruiter's message to the candidate: line breaks kept (one per line, <c>\n</c>), other
    /// control characters and the bidirectional overrides that can make text read backwards dropped,
    /// trimmed. Zero-width spaces and joiners stay — Burmese uses them between words. Null when nothing
    /// visible is left: an email that asks for something must say what.</summary>
    public static string? CleanMessage(string? typed)
    {
        if (typed is null) return null;
        var kept = new string(typed.ReplaceLineEndings("\n")
            .Where(c => c == '\n' || (!char.IsControl(c) && !IsBidiControl(c)))
            .ToArray()).Trim();
        return kept.Any(c => !char.IsWhiteSpace(c) && CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.Format) ? kept : null;
    }

    private static bool IsBidiControl(char c) => c is (>= '\u202A' and <= '\u202E') or (>= '\u2066' and <= '\u2069');

    /// <summary>Documents can be sent and changed until the end of the joining date, in the company's time
    /// zone.</summary>
    public static bool IsWindowOpen(DateOnly joiningDate, DateTimeOffset now, string? timeZoneId) =>
        OfferPortal.Today(now, timeZoneId) <= joiningDate;

    /// <summary>An accepted offer's link works until the later of its reply-by and joining dates (§5b);
    /// an unanswered one, until its reply-by date (§5a).</summary>
    public static DateOnly LinkOpenUntil(DateOnly expiresOn, DateOnly joiningDate) =>
        joiningDate > expiresOn ? joiningDate : expiresOn;
}
