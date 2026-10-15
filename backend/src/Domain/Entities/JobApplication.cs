using RecruitOps.Domain.Common;
using RecruitOps.Domain.Enums;

namespace RecruitOps.Domain.Entities;

/// <summary>A candidate's application to a job posting; tracks the pipeline stage.
/// Client feedback is gone — evaluation now happens through interview scorecards (Module 3).
/// <para>Named JobApplication, not Application, to avoid colliding with the
/// RecruitOps.Application namespace (Clean Architecture layer).</para></summary>
public class JobApplication : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid JobPostingId { get; set; }
    public Guid CandidateId { get; set; }

    public PipelineStatus Status { get; set; } = PipelineStatus.Sourced;

    /// <summary>Where this application came in from — needed by Module 5 to answer
    /// "which channel actually produces hires", which is the whole point of tracking it.</summary>
    public SourceChannel Source { get; set; } = SourceChannel.Direct;

    public DateTimeOffset AppliedAt { get; set; }

    /// <summary>Answers to the posting's customer-defined fields (Module 2.2), as JSONB.</summary>
    public string? CustomFieldsJson { get; set; }

    public string? CoverNote { get; set; }

    /// <summary>CV / Resume file key in object storage (R2/MinIO).</summary>
    public string? ResumeFileKey { get; set; }

    /// <summary>Original file name of the uploaded CV / Resume.</summary>
    public string? ResumeFileName { get; set; }

    /// <summary>Extracted text from candidate's CV / Resume.</summary>
    public string? ResumeExtractedText { get; set; }

    /// <summary>Timestamp when the resume was uploaded.</summary>
    public DateTimeOffset? ResumeUploadedAt { get; set; }

    /// <summary>Indicates whether extracted resume text was normalized from Zawgyi to Unicode NFC.</summary>
    public bool IsZawgyiNormalized { get; set; }
}
