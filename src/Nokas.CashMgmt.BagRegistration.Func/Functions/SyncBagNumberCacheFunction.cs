using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Nokas.CashMgmt.BagRegistration.Func.Services;

namespace Nokas.CashMgmt.BagRegistration.Func.Functions;

public sealed class SyncBagNumberCacheFunction(
    IBagNumberCacheSyncService syncService,
    ILogger<SyncBagNumberCacheFunction> logger)
{
    [Function("SyncBagNumberCache")]
    public async Task RunAsync([TimerTrigger("0 */15 * * * *")] TimerInfo timer, FunctionContext context)
    {
        try
        {
            await syncService.RunAsync(context.CancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Bag-number cache sync run failed.");
            throw;
        }
    }
}
