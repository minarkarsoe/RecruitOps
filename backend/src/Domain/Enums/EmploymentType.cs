namespace RecruitOps.Domain.Enums;

/// <summary>Contract shape shown on the public job page. Persisted as a string, so
/// reordering is safe; renaming a member is a data migration.</summary>
public enum EmploymentType
{
    FullTime,
    PartTime,
    Contract,
    Internship,
    Temporary,
}
