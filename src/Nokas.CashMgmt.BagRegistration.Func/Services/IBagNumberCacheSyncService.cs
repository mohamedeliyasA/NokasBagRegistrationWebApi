namespace Nokas.CashMgmt.BagRegistration.Func.Services;

public interface IBagNumberCacheSyncService
{
    Task RunAsync(CancellationToken cancellationToken);
}
