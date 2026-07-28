using RecruitOps.Domain.Common;

namespace RecruitOps.Domain.Entities;

/// <summary>A user @tagged in a note.
///
/// <para>Rows are produced by <b>parsing the note body server-side</b> against users the
/// author can actually see — never taken from the request. A client-supplied mention list
/// would let someone forge a note that appears addressed to a colleague, and (once Module 7
/// adds notification delivery) make the system send on their behalf.</para>
/// </summary>
public class NoteMention : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid NoteId { get; set; }
    public Guid MentionedUserId { get; set; }
}
