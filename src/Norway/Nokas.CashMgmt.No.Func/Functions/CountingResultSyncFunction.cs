using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Nokas.CashMgmt.No.Func.Services;

namespace Nokas.CashMgmt.No.Func.Functions;

public sealed class CountingResultSyncFunction(
    ICountingResultSyncService syncService,
    ILogger<CountingResultSyncFunction> logger)
{
    [Function("CountingResultSync")]
    public async Task RunAsync([TimerTrigger("0 */30 * * * *")] TimerInfo timer, FunctionContext context)
    {
        try
        {
            await syncService.RunAsync(context.CancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Counting-result sync run failed.");
            throw;
        }
    }
}
