namespace RecruitOps.Application.Common;

/// <summary>A refusal written for the candidate on an anonymous page ("that code isn't right, 3 tries
/// left"). Its own type so the controller shows exactly these messages to strangers and nothing else —
/// an <see cref="InvalidOperationException"/> from a library ("Sequence contains no elements") must
/// never reach them (security review, 2026-10-23).</summary>
public sealed class PortalRefusalException : Exception
{
    public PortalRefusalException(string message) : base(message) { }
}
