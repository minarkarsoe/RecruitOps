namespace RecruitOps.Domain.Enums;

/// <summary>Where an application came from (Module 2.1, ADR-0032 §1). Stored by name, so values may be added.
/// <c>Direct</c> is the career page from no channel we can name; <c>ManualUpload</c> a CV a recruiter
/// uploaded; <c>ExcelImport</c> is unused — no spreadsheet import exists.</summary>
public enum SourceChannel { Direct, Facebook, LinkedIn, Telegram, Referral, ExcelImport, Viber, ManualUpload }
