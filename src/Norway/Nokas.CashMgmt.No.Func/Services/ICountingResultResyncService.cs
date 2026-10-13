namespace Nokas.CashMgmt.No.Func.Services;

public sealed record CountingResultResyncResult(bool CustomerFound, int RowsUpserted, int StaleDocsPurged);

/// <summary>
/// Internal repair/backfill tool - function-key auth only, never exposed via APIM/Swagger.
/// Shares the same (customerNumber, fromDate)-driven backfill code path the sync service uses
/// for newly onboarded customers - not a second implementation.
/// </summary>
public interface ICountingResultResyncService
{
    Task<CountingResultResyncResult> ResyncCustomerAsync(
        string customerNumber, DateTime? fromDate, bool purgeOtherClients, CancellationToken cancellationToken);
}
