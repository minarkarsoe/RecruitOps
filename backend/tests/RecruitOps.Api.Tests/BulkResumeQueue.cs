using Microsoft.Extensions.DependencyInjection;
using RecruitOps.Infrastructure.Services.Delivery;

namespace RecruitOps.Api.Tests;

/// <summary>Drives the bulk CV queue deliberately, instead of sleeping and hoping.
///
/// <para><b>Why this exists.</b> These suites used to do <c>await Task.Delay(300)</c> and then
/// assert that a background <c>Task.Run</c> had finished — which is a bet on the machine being
/// fast, re-placed on every run. The rewrite onto ADR-0026 makes the work claimable, so a test can
/// ask for a pass and know exactly what happened when it returns. Nothing here waits on wall-clock
/// time.</para>
/// </summary>
internal static class BulkResumeQueue
{
    /// <summary>Runs passes until the queue stops producing work, and returns how many files were
    /// handled in total.
    ///
    /// <para>A loop rather than a single pass because <c>BulkResumeOptions.BatchSize</c> is 5 and a
    /// test may queue thirty files. Bounded, so a bug that makes a row immortal fails the test
    /// instead of hanging the suite — which is the whole reason the attempt cap exists.</para></summary>
    public static async Task<int> DrainAsync(CustomWebAppFactory factory, int maxPasses = 40)
    {
        var worker = factory.Services.GetRequiredService<BulkResumeWorker>();

        var handled = 0;
        for (var pass = 0; pass < maxPasses; pass++)
        {
            var thisPass = await worker.RunOnceAsync();
            if (thisPass == 0) return handled;
            handled += thisPass;
        }

        throw new InvalidOperationException(
            $"The bulk queue was still producing work after {maxPasses} passes. Either a row is "
            + "being reclaimed forever, or the test queued more files than this helper expects.");
    }
}
