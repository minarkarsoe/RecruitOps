using RecruitOps.Application.DTOs;

namespace RecruitOps.Application.Interfaces;

/// <summary>Module 2.1/2.2 — the anonymous applicant-facing surface.
///
/// <para>⚠️ Every method here runs <b>without a tenant claim</b>. The token is the only
/// thing that identifies a company, so implementations must read the token row with
/// <c>IgnoreQueryFilters()</c> and then constrain everything else to the tenant it
/// resolves to. Nothing on this interface may accept a tenant id from the caller.</para>
/// </summary>
public interface IPublicJobService
{
    /// <summary>The public view of a posting, or null if the token is unknown, revoked,
    /// expired, or the posting was never published.</summary>
    Task<PublicJobDto?> GetByTokenAsync(string token, CancellationToken ct = default);

    /// <summary>Records an application against the token's posting, reusing an existing
    /// candidate when email or phone matches (Module 2.7). Returns null when the token is
    /// not usable; throws InvalidOperationException when the posting is no longer open.</summary>
    Task<SubmitApplicationResponse?> SubmitAsync(
        string token, SubmitApplicationRequest request, CancellationToken ct = default);
}
