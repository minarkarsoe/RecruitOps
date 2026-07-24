namespace RecruitOps.Application.Common;

/// <summary>Resolves the current request's tenant so Infrastructure can enforce isolation.</summary>
public interface ICurrentTenant
{
    Guid TenantId { get; }
}
