namespace RecruitOps.Domain.Enums;

// Candidate pipeline vocabulary — do not invent new labels (design system §5.2).
public enum PipelineStatus { Sourced, Shortlisted, SentToClient, Interview, Placed, Rejected }
