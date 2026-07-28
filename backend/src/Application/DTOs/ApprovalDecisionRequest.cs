using System.ComponentModel.DataAnnotations;

namespace RecruitOps.Application.DTOs;

public record ApprovalDecisionRequest
{
    /// <summary>True to approve, false to reject.</summary>
    [Required]
    public bool Approve { get; init; }

    [StringLength(1000)]
    public string? Comment { get; init; }
}
