namespace RecruitOps.Application.Interfaces;

/// <summary>Per-account brute-force protection for login.
///
/// <para>This sits <em>alongside</em> the per-IP rate limiter in the API, not instead of it.
/// The IP limiter stops one machine hammering the endpoint; this stops a credential-stuffing
/// run that spreads the same account's guesses across many IPs, which the IP limiter cannot
/// see. Neither alone is sufficient.</para>
///
/// <para><b>Failures are counted for every email, whether or not the account exists.</b>
/// If only real accounts were counted, the 429 itself would tell an attacker which emails
/// are registered — reintroducing the user enumeration that <c>AuthService</c> deliberately
/// avoids by returning the same 401 for both cases.</para>
/// </summary>
public interface ILoginThrottle
{
    /// <summary>Returns the remaining lockout period, or null if the account may attempt a login.</summary>
    TimeSpan? RetryAfter(string email);

    /// <summary>Records a failed attempt. Reaching the threshold starts the lockout.</summary>
    void RecordFailure(string email);

    /// <summary>Clears the counter after a successful login, so a user who mistypes twice
    /// and then succeeds isn't one slip away from being locked out later.</summary>
    void Reset(string email);
}
