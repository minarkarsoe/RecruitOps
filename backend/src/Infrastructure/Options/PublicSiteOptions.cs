namespace RecruitOps.Infrastructure.Options;

/// <summary>Where candidates reach the public site — the base of every link this product emails them
/// (<c>{BaseUrl}/offer/{token}</c>). The API cannot work this out from a request: links are written by
/// the delivery worker, which has none.</summary>
public class PublicSiteOptions
{
    public const string SectionName = "PublicSite";

    /// <summary>e.g. <c>https://careers.example.com</c>. Empty means "not configured", and sending an offer
    /// is then refused rather than emailing a link that goes nowhere (ADR-0030 §5a).</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>The configured base without a trailing slash, or null when unset or not an absolute
    /// http(s) URL.</summary>
    public string? Normalized()
    {
        var value = BaseUrl.Trim().TrimEnd('/');
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http" ? value : null;
    }

    /// <summary>A published posting's advert, <c>{BaseUrl}/jobs/{token}</c> — or null without a token or a
    /// configured base. The SPA then links on its own origin, where the production nginx serves
    /// <c>/jobs/</c> (ADR-0029); it never guesses a port.</summary>
    public string? JobUrl(string? token) =>
        string.IsNullOrEmpty(token) || Normalized() is not { } site ? null : $"{site}/jobs/{Uri.EscapeDataString(token)}";
}
