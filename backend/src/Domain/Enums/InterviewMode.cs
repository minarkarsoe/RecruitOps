namespace RecruitOps.Domain.Enums;

/// <summary>How the interview is conducted. Drives what <c>Interview.Location</c> means —
/// a room for <see cref="OnSite"/>, a meeting URL for <see cref="Video"/>, a number for
/// <see cref="Phone"/> — which is why it is an enum and not free text.</summary>
public enum InterviewMode
{
    OnSite,
    Video,
    Phone,
}
