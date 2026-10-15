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

    /// <summary>Clients remembered at once. Past it, new clients are neither tracked nor recorded
    /// until the window frees space — the log loses detail under a flood, the process keeps its
    /// memory (security review of ADR-0028, L4; <c>LoginThrottle</c> caps the same way).</summary>
    public const int MaxTrackedClients = 50_000;

    private static readonly System.Text.RegularExpressions.Regex PlainEmail = new(
        @"^[a-z0-9._%+-]{1,64}@(?:[a-z0-9-]{1,63}\.)+[a-z]{2,63}\z",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private DateTimeOffset _lastPrune;

    /// <summary>For tests and diagnostics.</summary>
    public int TrackedClients { get { lock (_gate) return _clients.Count; } }

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

            // Prune by time, on any path, at most once a minute — the recording path alone stops
            // running once the hourly budget is spent, which is exactly when a flood arrives.
            if (_clients.Count >= MaxTrackedClients && now - _lastPrune >= TimeSpan.FromMinutes(1))
            {
                Prune(now);
                _lastPrune = now;
            }
            if (_clients.Count >= MaxTrackedClients && !_clients.ContainsKey(client))
                return (false, 0);

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
            return (true, carried);
        }
    }

    /// <summary>The text typed (lowercased), if it is plainly an email address; otherwise null — and then it
    /// is not kept at all (security review of ADR-0027, M2). The sign-in field takes anything with
    /// an "@", people paste passwords into the wrong box, and an audit entry is forever. Control,
    /// whitespace and invisible formatting characters (bidi overrides) are refused outright: they
    /// would let a caller change what an auditor reads.</summary>
    public static string? PlainEmailOrNull(string typed)
    {
        // Plain ASCII only, of the shape people's work addresses have. MailAddress accepted quoted
        // local parts and any letters, which let punctuation, formulas and look-alike scripts
        // into an auditor's view (security review of ADR-0028, L6).
        return typed.Length <= 254 && PlainEmail.IsMatch(typed) ? typed : null;
    }

    /// <summary>Forgets every client whose window has passed — with whatever it had suppressed, which
    /// is detail lost, not safety: the next attempt from it starts a fresh window and is recorded.</summary>
    private void Prune(DateTimeOffset now)
    {
        foreach (var key in _clients.Where(kv => now - kv.Value.WindowStart >= Window).Select(kv => kv.Key).ToList())
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
