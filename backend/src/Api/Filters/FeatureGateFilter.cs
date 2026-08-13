using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

using RecruitOps.Api.Attributes;
using RecruitOps.Application.Interfaces;

namespace RecruitOps.Api.Filters;

public class FeatureGateFilter : IAsyncActionFilter
{
    private readonly IFeatureFlagService _featureFlagService;

    public FeatureGateFilter(IFeatureFlagService featureFlagService)
    {
        _featureFlagService = featureFlagService;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var attributes = context.ActionDescriptor.EndpointMetadata.OfType<FeatureGateAttribute>().ToList();

        if (attributes.Count > 0)
        {
            foreach (var attr in attributes)
            {
                var isEnabled = await _featureFlagService.IsEnabledAsync(attr.FeatureName, context.HttpContext.RequestAborted);
                if (!isEnabled)
                {
                    context.Result = new ObjectResult(new
                    {
                        error = "FeatureDisabled",
                        featureName = attr.FeatureName,
                        message = $"The feature '{attr.FeatureName}' is disabled for this installation."
                    })
                    {
                        StatusCode = StatusCodes.Status403Forbidden
                    };
                    return;
                }
            }
        }

        await next();
    }
}
