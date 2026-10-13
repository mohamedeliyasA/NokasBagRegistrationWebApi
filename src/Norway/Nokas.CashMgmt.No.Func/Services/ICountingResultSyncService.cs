using Nokas.CashMgmt.No.Func.Models;

namespace Nokas.CashMgmt.No.Func.Services;

public interface ICountingResultSyncService
{
    Task RunAsync(CancellationToken cancellationToken);

    /// <summary>The shared (customerNumber, fromDate)-driven backfill path - used both for
    /// newly onboarded customers during a normal run and by the resync function. Returns the
    /// number of rows upserted.</summary>
    Task<int> BackfillCustomerAsync(CustomerMappingRow mapping, DateTime fromDate, CancellationToken cancellationToken);
}
