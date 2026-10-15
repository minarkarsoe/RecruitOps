namespace RecruitOps.Application.Interfaces;

public interface IFeatureFlagService
{
    Task<bool> IsEnabledAsync(string featureName, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, bool>> GetAllFlagsAsync(CancellationToken cancellationToken = default);
}
