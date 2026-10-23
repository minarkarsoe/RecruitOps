using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RecruitOps.Api.Auth;
using RecruitOps.Application.Common;
using RecruitOps.Application.DTOs;
using RecruitOps.Application.Interfaces;

namespace RecruitOps.Api.Controllers;

/// <summary>Module 4.3 — the candidate's offer link (ADR-0030 §5a), drawn in
/// <c>design/public/offer-portal.html</c>.
///
/// <para>⚠️ Anonymous, rate limited, and uninformative in the same way as the job pages: an unknown,
/// malformed or never-sent token is one 404. Beyond the token, the session cookie a correct code sets is
/// what reads the offer and answers it — a missing or expired one is a 401, and the page goes back to
/// the code step.</para>
///
/// <para>After accepting, the same session sends joining documents (§5b) until the joining date.</para>
///
/// <para>The cookie is <c>HttpOnly</c> (the page's script never holds it), <c>SameSite=Strict</c>, scoped
/// to this offer's path, <c>Secure</c> everywhere but local development, and a browser-session cookie: it
/// has no expiry of its own, so closing the browser ends it — the server ends it after 30 minutes or at
/// an answer, whichever is first (security review M4, 2026-10-23).</para></summary>
[ApiController]
[Route("api/public/offers/{token}")]
[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicies.OfferPortal)]
[RequestSizeLimit(16 * 1024)] // the largest legitimate body is a 1,000-character reason — but for an upload, below
public class PublicOffersController : ControllerBase
{
    public const string SessionCookie = "offer_session";

    private readonly IOfferPortalService _portal;
    private readonly IRequestOrigin _origin;
    private readonly IWebHostEnvironment _env;

    public PublicOffersController(IOfferPortalService portal, IRequestOrigin origin, IWebHostEnvironment env)
    {
        _portal = portal;
        _origin = origin;
        _env = env;
    }

    /// <summary>What the link alone shows: the company, where a code would go, and whether it has expired.</summary>
    [HttpGet]
    public async Task<ActionResult<PortalOfferLinkDto>> Open(string token, CancellationToken ct)
    {
        var link = await _portal.OpenAsync(token, ct);
        return link is null ? NotFound() : Ok(link);
    }

    [HttpPost("code")]
    public Task<IActionResult> SendCode(string token, CancellationToken ct) => Guarded(async () =>
        await _portal.SendCodeAsync(token, ct) ? Accepted() : NotFound());

    [HttpPost("verify")]
    public Task<IActionResult> Verify(string token, VerifyOfferCodeRequest request, CancellationToken ct) => Guarded(async () =>
    {
        if (await _portal.VerifyAsync(token, request.Code, ct) is not { } session) return NotFound();

        Response.Cookies.Append(SessionCookie, session.Token, new CookieOptions
        {
            HttpOnly = true,
            // Not only when the request says HTTPS: an install whose proxy does not forward the scheme
            // would otherwise fail open. Plain HTTP is for a developer's machine only.
            Secure = Request.IsHttps || !_env.IsDevelopment(),
            SameSite = SameSiteMode.Strict,
            Path = $"/api/public/offers/{token}",
            IsEssential = true,
        });
        return NoContent();
    });

    [HttpGet("offer")]
    public Task<IActionResult> GetOffer(string token, CancellationToken ct) =>
        Guarded(async () => Found(await _portal.GetOfferAsync(token, Session(), ct)));

    [HttpPost("accept")]
    public Task<IActionResult> Accept(string token, AcceptOfferRequest request, CancellationToken ct) =>
        Guarded(async () => Found(await _portal.AcceptAsync(
            token, Session(), request, _origin.IpAddress, Request.Headers.UserAgent.ToString(), ct)));

    [HttpPost("decline")]
    public Task<IActionResult> Decline(string token, DeclineOfferRequest request, CancellationToken ct) =>
        Guarded(async () => Found(await _portal.DeclineAsync(token, Session(), request, ct)));

    // ---------- joining documents, after accepting (ADR-0030 §5b) ----------

    [HttpGet("documents")]
    public Task<IActionResult> Documents(string token, CancellationToken ct) =>
        Guarded(async () => Found(await _portal.GetDocumentsAsync(token, Session(), ct)));

    /// <summary>One file, multipart, field <c>file</c>. The only large body this controller takes: 10 MB
    /// of file plus the multipart framing. The type is decided by the service from the file's first
    /// bytes.</summary>
    [HttpPost("documents/{kind}/files")]
    [RequestSizeLimit(UploadLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadLimit)]
    public Task<IActionResult> Upload(string token, string kind, IFormFile? file, CancellationToken ct) => Guarded(async () =>
    {
        if (file is null) return BadRequest(new ProblemDetails { Title = "Cannot continue", Detail = "Choose a file to upload." });
        await using var content = file.OpenReadStream();
        return Found(await _portal.UploadAsync(token, Session(), kind, file.FileName, file.Length, content, ct));
    });

    [HttpDelete("documents/files/{fileId:guid}")]
    public Task<IActionResult> RemoveFile(string token, Guid fileId, CancellationToken ct) =>
        Guarded(async () => Found(await _portal.RemoveFileAsync(token, Session(), fileId, ct)));

    [HttpPut("documents/bank")]
    public Task<IActionResult> SaveBankDetails(string token, SaveBankDetailsRequest request, CancellationToken ct) =>
        Guarded(async () => Found(await _portal.SaveBankDetailsAsync(token, Session(), request, ct)));

    [HttpPost("documents/send")]
    public Task<IActionResult> SendForChecking(string token, CancellationToken ct) =>
        Guarded(async () => Found(await _portal.SendForCheckingAsync(token, Session(), ct)));

    /// <summary>10 MB of file (<c>Preboarding.MaxFileBytes</c>) and room for the multipart framing.</summary>
    private const long UploadLimit = 10 * 1024 * 1024 + 64 * 1024;

    private string? Session() => Request.Cookies.TryGetValue(SessionCookie, out var value) ? value : null;

    private IActionResult Found(PortalOfferDto? offer) => offer is null ? NotFound() : Ok(offer);

    private IActionResult Found(PortalDocumentsDto? documents) => documents is null ? NotFound() : Ok(documents);

    /// <summary>The service's refusals, as the candidate should read them: its messages are written for
    /// them (409), and a missing session sends the page back to the code step (401). Only
    /// <see cref="PortalRefusalException"/> carries text to a stranger; anything else is a 500 with
    /// nothing in it.</summary>
    private async Task<IActionResult> Guarded(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
        catch (PortalRefusalException ex)
        {
            return Conflict(new ProblemDetails { Title = "Cannot continue", Detail = ex.Message });
        }
    }
}
