using System.Net;
using System.Net.Sockets;

namespace RecruitOps.Infrastructure.Services;

/// <summary>Decides which failed sign-ins for <b>unknown</b> emails get an audit entry (ADR-0027 §7).
///
/// <para>The per-account login throttle cannot bound these: it is keyed on the email, and an
/// attacker who tries a new random email every time never reaches it. Recording each attempt
/// would let anyone fill a table that, by design, nobody can prune (security review of ADR-0027,
/// M1). So per client — an IPv4 address, or an IPv6 /64 — the first such failure in each
/// 15-minute window is recorded, carrying the count of the ones in between; and install-wide no
/// more than <see cref="MaxEntriesPerHour"/> are recorded an hour.</para>
///
/// <para>Failures for <b>real</b> accounts are always recorded: the account throttle already caps
/// them at five per fifteen minutes each, and they are the ones an account holder needs to see.</para>
///
/// <para>In-process, like <c>LoginThrottle</c>: right for one instance per company (ADR-0004).</para>
/// </summary>
public sealed class UnknownSignInAuditGate
{
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);
    public const int MaxEntriesPerHour = 60;

    private readonly TimeProvider _clock;
    private readonly object _gate = new();
    private readonly Dictionary<string, (DateTimeOffset WindowStart, int Suppressed)> _clients = new();
    private (DateTimeOffset HourStart, int Count) _hour;

    public UnknownSignInAuditGate(TimeProvider clock) => _clock = clock;

    /// <summary>Whether to record this attempt, and how many earlier ones from the same client went
    /// unrecorded since its last entry.</summary>
    public (bool Record, int SuppressedBefore) Admit(string? ipAddress)
    {
        var now = _clock.GetUtcNow();
        var client = ClientKey(ipAddress);

        lock (_gate)
        {
            if (now - _hour.HourStart >= TimeSpan.FromHours(1)) _hour = (now, 0);

            if (_clients.TryGetValue(client, out var seen) && now - seen.WindowStart < Window)
            {
                _clients[client] = (seen.WindowStart, seen.Suppressed + 1);
                return (false, 0);
            }

            var carried = seen.Suppressed;
            if (_hour.Count >= MaxEntriesPerHour)
            {
                _clients[client] = (seen.WindowStart == default ? now : seen.WindowStart, carried + 1);
                return (false, 0);
            }

            _hour = (_hour.HourStart, _hour.Count + 1);
            _clients[client] = (now, 0);
            if (_clients.Count > 10_000) Prune(now);
            return (true, carried);
        }
    }

    /// <summary>The text typed, if it is unmistakably an email address; otherwise null — and then it
    /// is not kept at all (security review of ADR-0027, M2). The sign-in field takes anything with
    /// an "@", people paste passwords into the wrong box, and an audit entry is forever. Control,
    /// whitespace and invisible formatting characters (bidi overrides) are refused outright: they
    /// would let a caller change what an auditor reads.</summary>
    public static string? PlainEmailOrNull(string typed)
    {
        if (typed.Length is 0 or > 254) return null;
        if (typed.Any(c => char.IsControl(c) || char.IsWhiteSpace(c)
                           || char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.Format))
            return null;
        if (!System.Net.Mail.MailAddress.TryCreate(typed, out var address) || address.Address != typed) return null;
        return address.Host.Contains('.') ? typed : null;
    }

    private void Prune(DateTimeOffset now)
    {
        foreach (var key in _clients.Where(kv => now - kv.Value.WindowStart >= Window && kv.Value.Suppressed == 0)
                                    .Select(kv => kv.Key).ToList())
            _clients.Remove(key);
    }

    private static string ClientKey(string? ipAddress)
    {
        if (!IPAddress.TryParse(ipAddress, out var ip)) return "unknown";
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        if (ip.AddressFamily != AddressFamily.InterNetworkV6) return ip.ToString();

        var bytes = ip.GetAddressBytes();
        Array.Clear(bytes, 8, 8); // one /64 is one client, as the login rate limiter treats it
        return new IPAddress(bytes) + "/64";
    }
}
