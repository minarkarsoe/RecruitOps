using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using RecruitOps.Application.Interfaces;

namespace RecruitOps.Infrastructure.Services;

/// <summary>In-memory implementation of <see cref="ILoginThrottle"/>. See ADR-0016.
///
/// <para><b>Deliberately in-process.</b> ADR-0004 delivers one instance per company, so a
/// process-local counter covers the whole deployment. It does <b>not</b> survive a restart,
/// and would not be shared across replicas — if we ever run more than one instance per
/// customer, this must move to Redis or a database table, or the effective limit silently
/// becomes N × the configured value.</para>
///
/// <para><b>Known trade-off — lockout as a denial of service.</b> Anyone who knows a
/// colleague's email can lock them out for the window by failing deliberately. The window is
/// therefore short (15 minutes, not hours) and there is no permanent lock or admin unlock
/// step. Slowing an attacker to 5 guesses per 15 minutes already defeats brute force; a
/// longer lockout buys almost nothing and hands out a griefing tool.</para>
///
/// <para><b>Why the key is hashed.</b> The email arrives from an anonymous request and is
/// attacker-controlled, so storing it verbatim would let anyone retain arbitrary amounts of
/// memory for the whole window by failing against very long addresses. A fixed-size hash
/// makes every entry cost the same, and has the side benefit that a memory dump of a
/// running server doesn't contain a list of attempted addresses.</para>
/// </summary>
public class LoginThrottle : ILoginThrottle
{
    /// <summary>Attempts allowed before the lockout starts.</summary>
    private const int MaxFailures = 5;

    /// <summary>How long failures are remembered, and how long a lockout lasts.</summary>
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    /// <summary>Hard ceiling on tracked accounts. This is a real bound, not a sweep trigger:
    /// once reached, new accounts stop being tracked until entries age out. Losing throttling
    /// for *new* accounts under a flood is strictly better than letting an anonymous endpoint
    /// dictate the process's memory usage — and the per-IP limiter still applies.</summary>
    private const int MaxTrackedAccounts = 50_000;

    /// <summary>Sweeps are time-based, not count-based. A count-based trigger is controlled by
    /// the attacker: parking 10k junk entries would make every subsequent failed login walk
    /// the whole dictionary.</summary>
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(1);

    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly TimeProvider _clock;
    private long _nextSweepTicks;

    public LoginThrottle(TimeProvider clock)
    {
        _clock = clock;
        _nextSweepTicks = clock.GetUtcNow().Add(SweepInterval).UtcTicks;
    }

    /// <summary>Every field is read and written under <see cref="Gate"/>. Reading
    /// <see cref="WindowStart"/> unlocked would be a torn read — DateTimeOffset is 16 bytes
    /// and its assignment is not atomic — which could silently clear a lockout or report a
    /// nonsense Retry-After.</summary>
    private sealed class Entry
    {
        public readonly object Gate = new();
        public int Failures;
        public DateTimeOffset WindowStart;
    }

    /// <summary>Fixed-size, case-insensitive key. Normalising here rather than relying on the
    /// dictionary's comparer means a future change to the database collation cannot silently
    /// let an attacker split one account's counter across casings.</summary>
    private static string KeyFor(string email)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
    }

    public TimeSpan? RetryAfter(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;

        var key = KeyFor(email);
        if (!_entries.TryGetValue(key, out var entry)) return null;

        var now = _clock.GetUtcNow();
        lock (entry.Gate)
        {
            var elapsed = now - entry.WindowStart;
            if (elapsed >= Window)
            {
                _entries.TryRemove(key, out _);
                return null;
            }

            return entry.Failures >= MaxFailures ? Window - elapsed : null;
        }
    }

    public void RecordFailure(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return;

        var key = KeyFor(email);
        var now = _clock.GetUtcNow();

        // GetOrAdd's factory is side-effect free; all mutation happens under the entry's
        // lock below. ConcurrentDictionary may invoke an AddOrUpdate delegate more than once
        // when its CAS retries, which for a "Failures++" delegate means over-counting — and
        // over-counting locks an account out in fewer attempts than the policy states.
        if (!_entries.TryGetValue(key, out var entry))
        {
            if (_entries.Count >= MaxTrackedAccounts)
            {
                Sweep(now);
                if (_entries.Count >= MaxTrackedAccounts) return;
            }
            entry = _entries.GetOrAdd(key, _ => new Entry { WindowStart = now });
        }

        lock (entry.Gate)
        {
            // A stale window is restarted rather than incremented, so occasional typos
            // spread over days never accumulate into a lockout.
            if (now - entry.WindowStart >= Window)
            {
                entry.Failures = 1;
                entry.WindowStart = now;
            }
            else
            {
                entry.Failures++;
            }
        }

        MaybeSweep(now);
    }

    public void Reset(string email)
    {
        if (!string.IsNullOrWhiteSpace(email)) _entries.TryRemove(KeyFor(email), out _);
    }

    private void MaybeSweep(DateTimeOffset now)
    {
        var next = Interlocked.Read(ref _nextSweepTicks);
        if (now.UtcTicks < next) return;

        // Only the thread that wins the exchange sweeps; the rest carry on.
        if (Interlocked.CompareExchange(
                ref _nextSweepTicks, now.Add(SweepInterval).UtcTicks, next) != next) return;

        Sweep(now);
    }

    private void Sweep(DateTimeOffset now)
    {
        foreach (var (key, entry) in _entries)
        {
            bool expired;
            lock (entry.Gate) expired = now - entry.WindowStart >= Window;
            if (expired) _entries.TryRemove(key, out _);
        }
    }
}
