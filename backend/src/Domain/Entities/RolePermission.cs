namespace RecruitOps.Domain.Entities;

/// <summary>Join entity connecting Role and Permission with assignment metadata (Requirement R2).</summary>
public class RolePermission
{
    public Guid RoleId { get; set; }
    public Role Role { get; set; } = null!;

    public Guid PermissionId { get; set; }
    public Permission Permission { get; set; } = null!;

    public DateTimeOffset AssignedAt { get; set; } = DateTimeOffset.UtcNow;
}
