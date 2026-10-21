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

    /// <summary>What the chain approves: <c>Requisition</c> (the default) or <c>Offer</c> (ADR-0030 §1).</summary>
    [RegularExpression("^(Requisition|Offer)$")]
    public string Purpose { get; init; } = "Requisition";

    /// <summary>Ordered. Sequence numbers are assigned from this order (1-based),
    /// so callers cannot create gaps or duplicates. At most 20: every submission against the
    /// chain writes one approval row per step.</summary>
    [Required, MinLength(1), MaxLength(20)]
    public IReadOnlyList<CreateApprovalChainStepRequest> Steps { get; init; } =
        Array.Empty<CreateApprovalChainStepRequest>();
}
