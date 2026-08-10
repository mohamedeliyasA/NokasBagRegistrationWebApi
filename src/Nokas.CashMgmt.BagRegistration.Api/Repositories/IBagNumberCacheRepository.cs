namespace Nokas.CashMgmt.BagRegistration.Api.Repositories;

public interface IBagNumberCacheRepository
{
    Task<bool> ExistsAsync(string bagNumber, CancellationToken cancellationToken);
    Task<DateTime?> GetLastSyncedAtAsync(CancellationToken cancellationToken);
}
