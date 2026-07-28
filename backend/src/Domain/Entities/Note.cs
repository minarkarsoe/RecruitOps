using RecruitOps.Domain.Common;

namespace RecruitOps.Domain.Entities;

/// <summary>A comment on a job application (3.4) — the in-system replacement for the
/// recruiter↔manager conversation that otherwise happens in chat and is lost.
///
/// <para><see cref="Body"/> is stored <b>raw</b> and escaped on output. Sanitising on the way
/// in destroys what the user actually typed and still fails the moment a second renderer
/// appears; escaping at the point of rendering is the boundary that holds.</para>
/// </summary>
public class Note : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid JobApplicationId { get; set; }

    /// <summary>Optionally pinned to one interview round, so debrief comments don't drift
    /// into one undifferentiated thread across a three-round loop.</summary>
    public Guid? InterviewId { get; set; }

    public Guid AuthorUserId { get; set; }

    public string Body { get; set; } = string.Empty;
}
