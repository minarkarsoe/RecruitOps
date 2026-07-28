using RecruitOps.Domain.Common;

namespace RecruitOps.Domain.Entities;

/// <summary>An internal user on the interview panel.
///
/// <para><b>This row is an access grant.</b> A panel member is very often a Hiring Manager
/// from another department, who under ADR-0003 can see nothing about this application at
/// all — so without an exception, the most ordinary panel there is (HR plus a technical
/// lead from the team that will own the hire) simply cannot be run.</para>
///
/// <para>The grant is deliberately narrow (ADR-0017 §4): read on <b>this one job
/// application</b>, its interviews, its notes, and whatever the blind-scoring rule allows.
/// It confers no department access, nothing about any other application, and <b>no
/// writes</b> — rescheduling and stage moves stay with department-scoped recruitment staff.
/// It is resolved in exactly one place, <c>IApplicationAccess</c>, because a rule spread
/// across three services is how this repo has already shipped the same bug three times.</para>
/// </summary>
public class InterviewParticipant : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid InterviewId { get; set; }
    public Guid UserId { get; set; }

    /// <summary>The person running the round. Informational — it grants nothing extra;
    /// the lead is blind to the panel's scores until they submit, like everyone else.</summary>
    public bool IsLead { get; set; }
}
