using System.ComponentModel.DataAnnotations;

namespace RecruitOps.Application.DTOs;

public record ApprovalDecisionRequest
{
    /// <summary>True to approve, false to reject.</summary>
    [Required]
    public bool Approve { get; init; }

    [StringLength(1000)]
    public string? Comment { get; init; }

    /// <summary>The approval round the decider was looking at. <b>Offers require it</b> (ADR-0030
    /// §1): a decision is refused if the offer was revised and resubmitted since — an approver must
    /// never approve terms they did not see. Requisitions ignore it.</summary>
    [Range(1, int.MaxValue)]
    public int? Round { get; init; }
}
