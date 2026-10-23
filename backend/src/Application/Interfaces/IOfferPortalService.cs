using RecruitOps.Application.DTOs;

namespace RecruitOps.Application.Interfaces;

/// <summary>The candidate's offer link (Module 4.3, ADR-0030 §5a). Anonymous: the token names the offer,
/// and with it the company. Every method returns null for a token that does not resolve to a sent offer
/// — one answer for unknown, malformed and never-sent, so nobody can probe which a token is.
/// <para>A refusal the candidate can act on ("that code isn't right, 3 tries left") is an
/// <see cref="InvalidOperationException"/> whose message is written for them. A missing or expired
/// session is an <see cref="UnauthorizedAccessException"/>: the page goes back to the code step. Every
/// method that reads or answers the offer checks the session itself.</para></summary>
public interface IOfferPortalService
{
    Task<PortalOfferLinkDto?> OpenAsync(string token, CancellationToken ct = default);

    /// <summary>Emails a new code to the address on the application, replacing any earlier one.</summary>
    Task<bool> SendCodeAsync(string token, CancellationToken ct = default);

    /// <summary>A correct code opens a session; the offer becomes Viewed the first time.</summary>
    Task<PortalSession?> VerifyAsync(string token, string code, CancellationToken ct = default);

    Task<PortalOfferDto?> GetOfferAsync(string token, string? session, CancellationToken ct = default);

    Task<PortalOfferDto?> AcceptAsync(
        string token, string? session, AcceptOfferRequest request, string? ipAddress, string? userAgent,
        CancellationToken ct = default);

    Task<PortalOfferDto?> DeclineAsync(string token, string? session, DeclineOfferRequest request, CancellationToken ct = default);
}
