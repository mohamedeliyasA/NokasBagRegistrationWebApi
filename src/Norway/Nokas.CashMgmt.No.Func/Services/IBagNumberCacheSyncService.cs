namespace Nokas.CashMgmt.No.Func.Services;

public interface IBagNumberCacheSyncService
{
    Task RunAsync(CancellationToken cancellationToken);
}
