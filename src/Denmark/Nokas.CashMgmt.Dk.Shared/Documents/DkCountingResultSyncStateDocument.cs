using System.Text.Json.Serialization;

namespace Nokas.CashMgmt.Dk.Shared.Documents;

/// <summary>
/// Single sentinel document holding the sync watermark and the outcome of the last run. The
/// watermark is what makes the sync incremental: each run asks ServiceNow for rows modified
/// since it. It also backs <c>dataAsOf</c> and gives operators a view of the last run.
/// </summary>
public sealed class DkCountingResultSyncStateDocument
{
    public const string SentinelId = "sync-state";

    [JsonPropertyName("id")]
    public string Id { get; init; } = SentinelId;

    /// <summary>Sync-state is global, not per-partner, but the container partitions on /clientId.</summary>
    [JsonPropertyName("clientId")]
    public string ClientId { get; init; } = SentinelId;

    [JsonPropertyName("lastSuccessfulRunUtc")]
    public DateTime? LastSuccessfulRunUtc { get; init; }

    /// <summary>
    /// End of the last successfully fetched window, in UTC. The next run starts here minus the
    /// configured overlap. Advanced only after a run completes, so a failed run re-reads rather
    /// than skipping.
    /// </summary>
    [JsonPropertyName("lastModifiedWatermarkUtc")]
    public DateTime? LastModifiedWatermarkUtc { get; init; }

    [JsonPropertyName("lastRunRowsFetched")]
    public int LastRunRowsFetched { get; init; }

    [JsonPropertyName("lastRunRowsUpserted")]
    public int LastRunRowsUpserted { get; init; }

    /// <summary>Rows whose kardex no registration has claimed yet, so no clientId could be attributed.</summary>
    [JsonPropertyName("lastRunRowsSkippedUnclaimed")]
    public int LastRunRowsSkippedUnclaimed { get; init; }

    [JsonPropertyName("docType")]
    public string DocType { get; init; } = "dkCountingResultSyncState";
}
