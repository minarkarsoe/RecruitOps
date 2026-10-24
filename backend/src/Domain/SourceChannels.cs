using RecruitOps.Domain.Enums;

namespace RecruitOps.Domain;

/// <summary>The channels an anonymous applicant may claim (ADR-0032 §1).</summary>
public static class SourceChannels
{
    /// <summary>The channel the public form resolved — by its exact name, and only one a link can carry.
    /// Anything else, <c>ManualUpload</c> included, is <c>Direct</c>: it is the browser's hint, and a
    /// candidate must not be able to pass for a recruiter's upload.</summary>
    public static SourceChannel FromPublic(string? value) => value switch
    {
        nameof(SourceChannel.Facebook) => SourceChannel.Facebook,
        nameof(SourceChannel.LinkedIn) => SourceChannel.LinkedIn,
        nameof(SourceChannel.Telegram) => SourceChannel.Telegram,
        nameof(SourceChannel.Viber) => SourceChannel.Viber,
        nameof(SourceChannel.Referral) => SourceChannel.Referral,
        _ => SourceChannel.Direct,
    };
}
