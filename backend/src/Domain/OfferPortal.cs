using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace RecruitOps.Domain;

/// <summary>The rules of the candidate's offer link (Module 4.3, ADR-0030 §5 and §5a), in one place so
/// the service, the tests and the numbers in the ADR cannot drift apart.
///
/// <para><b>What is a secret and what is not.</b> The link's token is an address — it opens a page
/// showing the company's name and a masked email address, nothing more. The 6-digit code, emailed to the
/// address the offer was sent to, is the credential; the session cookie it buys is the second. The code
/// is stored only as an HMAC under a key that is not in the database — a plain hash of six digits is
/// reversed in microseconds by anyone who can read a backup (security review, 2026-10-23); the session,
/// 256 random bits, as a SHA-256.</para></summary>
public static class OfferPortal
{
    public const int CodeLength = 6;

    /// <summary>How long an emailed code works.</summary>
    public static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);

    /// <summary>Wrong tries before a code stops working and a new one must be sent.</summary>
    public const int MaxCodeAttempts = 5;

    /// <summary>Codes per offer per rolling 24 hours. With <see cref="MaxCodeAttempts"/>: 25 guesses a
    /// day at a one-in-a-million code, and the candidate is emailed every code anyone triggers.</summary>
    public const int MaxCodesPerDay = 5;

    public static readonly TimeSpan CodeWindow = TimeSpan.FromHours(24);

    /// <summary>How long a correct code keeps the offer open in that browser — long enough to read a
    /// letter, short enough that a shared computer does not stay signed in to someone's offer (security
    /// review, 2026-10-23: it was 2 hours). Answering ends it sooner.</summary>
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromMinutes(30);

    /// <summary>The shortest gap between two reminders of one offer.</summary>
    public static readonly TimeSpan ReminderInterval = TimeSpan.FromHours(24);

    public const int MaxSignatureNameLength = 200;

    public const int MaxDeclineReasonLength = 1000;

    /// <summary>32 random bytes, base64url without padding: 43 characters.</summary>
    public static string NewToken() => Base64Url(RandomNumberGenerator.GetBytes(32));

    /// <summary>Whether a string could be a token at all — checked before any lookup, so a malformed
    /// path never reaches the database.</summary>
    public static bool LooksLikeToken(string? token) =>
        token is { Length: 43 } && token.All(c => c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-' or '_');

    /// <summary>Six digits from a cryptographic generator, leading zeros kept.</summary>
    public static string NewCode() =>
        RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);

    /// <summary>The code as typed, reduced to its six digits — in ASCII whatever the script (a Myanmar
    /// keyboard types ၀–၉), with spaces and dashes ignored. Null when it is not six digits.</summary>
    public static string? NormalizeCode(string? typed)
    {
        if (string.IsNullOrWhiteSpace(typed)) return null;
        var digits = new StringBuilder(CodeLength);
        foreach (var c in typed)
        {
            var value = CharUnicodeInfo.GetDecimalDigitValue(c);
            if (value >= 0) digits.Append((char)('0' + value));
            else if (!char.IsWhiteSpace(c) && c != '-') return null;
            if (digits.Length > CodeLength) return null;
        }
        return digits.Length == CodeLength ? digits.ToString() : null;
    }

    public static string Sha256Hex(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    /// <summary>HMAC-SHA-256 under <paramref name="key"/>, which must not be stored with the hash; salted
    /// with the row's id, so two rows holding the same code never share a hash.</summary>
    public static string CodeHash(byte[] key, Guid accessId, string code) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes($"{accessId}:{code}")));

    /// <summary>Compares two hex hashes in constant time.</summary>
    public static bool HashesEqual(string expected, string actual) =>
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(actual));

    /// <summary><c>k•••••@gmail.com</c>: enough for the candidate to recognise their own address, not
    /// enough to tell a stranger holding the link what it is.</summary>
    public static string MaskEmail(string email)
    {
        var at = email.LastIndexOf('@');
        if (at <= 0) return "•••••";
        return $"{email[0]}•••••{email[at..]}";
    }

    /// <summary>The typed signature on one line, or null when there is nothing left of it or it is too
    /// long. A line break in it would read as two names in the record.</summary>
    public static string? CleanSignatureName(string? typed)
    {
        if (typed is null) return null;
        var name = OfferLetter.OneLine(typed);
        return name.Length is > 0 and <= MaxSignatureNameLength ? name : null;
    }

    /// <summary>Today's date in the company's time zone, or UTC when it has none or names one this
    /// machine does not know.</summary>
    public static DateOnly Today(DateTimeOffset now, string? timeZoneId)
    {
        var zone = TimeZoneInfo.Utc;
        if (!string.IsNullOrWhiteSpace(timeZoneId))
        {
            try
            {
                zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                zone = TimeZoneInfo.Utc;
            }
        }
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
    }

    /// <summary>The link works to the end of the expiry date, in the company's time zone.</summary>
    public static bool IsPastExpiry(DateOnly expiresOn, DateTimeOffset now, string? timeZoneId) =>
        Today(now, timeZoneId) > expiresOn;

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
