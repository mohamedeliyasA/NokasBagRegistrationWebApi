using Microsoft.ApplicationInsights;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Nokas.CashMgmt.Dk.BagRegistration.Func.Services;

namespace Nokas.CashMgmt.Dk.BagRegistration.Func.Functions;

/// <summary>
/// Every 10 minutes, and once at startup so a deploy does not leave the data stale for a whole
/// interval. A timer trigger runs on exactly one instance however many are running, which is why
/// this moved out of the API.
/// </summary>
public sealed class CountingResultSyncFunction(
    DkCountingResultSyncService sync,
    TelemetryClient telemetry,
    ILogger<CountingResultSyncFunction> logger)
{
    [Function("DkCountingResultSync")]
    public async Task RunAsync([TimerTrigger("0 */10 * * * *", RunOnStartup = true)] TimerInfo timer, CancellationToken cancellationToken)
    {
        try
        {
            await sync.RunOnceAsync(cancellationToken);

            if (sync.Telemetry.Count > 0)
            {
                telemetry.TrackEvent("DkCountingResultSyncRun", new Dictionary<string, string>(sync.Telemetry));
            }
        }
        catch (Exception ex)
        {
            // Swallowed on purpose: the watermark only advances on a completed run, so the next
            // tick retries the same window. Letting this throw would just add a noisy failure.
            logger.LogCritical(ex, "DK counting-result sync run failed.");
        }

        if (timer.ScheduleStatus is not null)
        {
            logger.LogDebug("Next DK counting-result sync at {Next:O}.", timer.ScheduleStatus.Next);
        }
    }
}
