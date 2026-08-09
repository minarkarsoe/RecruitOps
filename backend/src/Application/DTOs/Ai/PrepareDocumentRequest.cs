namespace RecruitOps.Application.DTOs.Ai;

public record PrepareDocumentRequest(
    Guid CandidateId,
    Guid JobPostingId,
    string DocumentType // "InterviewKit" | "ClientDossier" | "JdDraft"
);

public record DocumentPrepResultDto(
    string DocumentTitle,
    string ContentMarkdown,
    string ContentHtml
);
