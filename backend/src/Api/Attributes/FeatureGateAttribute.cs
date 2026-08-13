namespace RecruitOps.Api.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public class FeatureGateAttribute : Attribute
{
    public string FeatureName { get; }

    public FeatureGateAttribute(string featureName)
    {
        FeatureName = featureName ?? throw new ArgumentNullException(nameof(featureName));
    }
}
