using Nokas.CashMgmt.BagRegistration.Api.Repositories;

namespace Nokas.CashMgmt.BagRegistration.Api.Tests.Controllers;

public sealed class FakeBagNumberCacheRepository : IBagNumberCacheRepository
{
    public bool Exists { get; set; }
    public DateTime? LastSyncedAt { get; set; }

    public Task<bool> ExistsAsync(string bagNumber, CancellationToken cancellationToken) => Task.FromResult(Exists);

    public Task<DateTime?> GetLastSyncedAtAsync(CancellationToken cancellationToken) => Task.FromResult(LastSyncedAt);
}
