using System.ComponentModel.DataAnnotations;

namespace RecruitOps.Application.DTOs;

public record CreateApprovalChainStepRequest
{
    [Required]
    public Guid ApproverUserId { get; init; }

    [Required, StringLength(100, MinimumLength = 1)]
    public string Label { get; init; } = string.Empty;
}

public record CreateApprovalChainRequest
{
    [Required, StringLength(200, MinimumLength = 2)]
    public string Name { get; init; } = string.Empty;

    /// <summary>Null creates the company-wide default chain.</summary>
    public Guid? DepartmentId { get; init; }

    /// <summary>Ordered. Sequence numbers are assigned from this order (1-based),
    /// so callers cannot create gaps or duplicates.</summary>
    [Required, MinLength(1)]
    public IReadOnlyList<CreateApprovalChainStepRequest> Steps { get; init; } =
        Array.Empty<CreateApprovalChainStepRequest>();
}
