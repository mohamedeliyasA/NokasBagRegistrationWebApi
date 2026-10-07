using Nokas.CashMgmt.Dk.Shared.Documents;

namespace Nokas.CashMgmt.Dk.Shared.Repositories;

public sealed record DkCountingResultQueryOptions(
    DateTime FromDate, DateTime ToDate, string DateType, string? Status, string? CustomerRefNumber, int PageSize);

public sealed record DkCountingResultPage(IReadOnlyList<DkCountingResultDocument> Items, string? ContinuationToken);

public interface IDkCountingResultRepository
{
    Task<DkCountingResultPage> QueryAsync(
        string clientId, DkCountingResultQueryOptions query, string? nativeContinuationToken, CancellationToken cancellationToken);

    Task<DkCountingResultDocument?> GetLatestByBagNumberAsync(string clientId, string bagNumber, CancellationToken cancellationToken);

    Task<DateTime?> GetDataAsOfAsync(CancellationToken cancellationToken);

    /// <summary>End of the last successfully fetched window, or null before the first run.</summary>
    Task<DateTime?> GetWatermarkAsync(CancellationToken cancellationToken);

    /// <summary>Upserts a synced record. Used only by the sync background service.</summary>
    Task UpsertAsync(DkCountingResultDocument document, CancellationToken cancellationToken);

    /// <summary>Records the outcome of a sync run onto the sentinel sync-state document.</summary>
    Task RecordSyncRunAsync(
        DateTime completedAtUtc, DateTime watermarkUtc, int rowsFetched, int rowsUpserted, int rowsSkippedUnclaimed, CancellationToken cancellationToken);
}
