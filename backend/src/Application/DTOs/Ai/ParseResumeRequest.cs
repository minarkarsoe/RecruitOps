namespace RecruitOps.Application.DTOs.Ai;

public record ParseResumeRequest(
    string ResumeText,
    string? FileName
);

public record ParsedResumeResultDto(
    string FullName,
    string? Email,
    string? Phone,
    string Summary,
    List<WorkExperienceDto> WorkExperiences,
    List<EducationDto> Educations,
    List<string> Skills,
    List<string> Languages,
    int EstimatedYearsOfExperience
);

public record WorkExperienceDto(
    string Company,
    string Position,
    string StartDate,
    string EndDate,
    string Description,
    List<string> Highlights
);

public record EducationDto(
    string Institution,
    string Degree,
    string FieldOfStudy,
    string StartDate,
    string EndDate
);
