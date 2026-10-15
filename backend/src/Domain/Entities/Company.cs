using RecruitOps.Domain.Common;

namespace RecruitOps.Domain.Entities;

/// <summary>The customer company. One row per deployment — each company gets its own
/// database (ADR-0004). Holds branding/settings used by job pages and emails.</summary>
public class Company : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Subdomain slug used for routing (ADR-0004).</summary>
    public string Slug { get; set; } = string.Empty;

    public string? LogoUrl { get; set; }

    /// <summary>The company's own working time zone, as an IANA id — <c>"Asia/Yangon"</c>.
    ///
    /// <para><b>What breaks without it.</b> An interview slot is stored as <c>timestamptz</c>,
    /// which normalises to UTC and discards the offset the recruiter's browser sent. So the
    /// instant survives a round-trip and "09:00" does not — and 09:00 is the only part of an
    /// invitation the candidate acts on. UTC+06:30 is far enough from UTC to move a Monday
    /// morning interview into Sunday evening when rendered wrong.</para>
    ///
    /// <para>Null falls back to UTC, and the email says so out loud rather than quietly writing a
    /// time in the wrong zone. Same reasoning as <see cref="ScheduledJob.TimeZoneId"/>.</para></summary>
    public string? TimeZoneId { get; set; }

    public bool IsActive { get; set; } = true;
}
