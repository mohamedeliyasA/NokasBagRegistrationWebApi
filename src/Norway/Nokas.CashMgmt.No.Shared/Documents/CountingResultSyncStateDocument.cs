using System.Text.Json.Serialization;

namespace Nokas.CashMgmt.No.Shared.Documents;

/// <summary>
/// Single sync-state doc in the `countingresults` container tracking the two independent
/// watermarks the sync advances. Only advanced after a run commits in full. Distinct from
/// <see cref="SyncStateDocument"/> (bag-number cache), which has a single watermark and lives
/// in a different container.
/// </summary>
public sealed class CountingResultSyncStateDocument
{
    public const string DocumentId = "sync-state";

    /// <summary>Sentinel partition value - the countingresults container is partitioned by
    /// /clientId, but this document is global, not scoped to any one partner.</summary>
    public const string SentinelClientId = "_syncstate";

    [JsonPropertyName("id")]
    public string Id { get; init; } = DocumentId;

    [JsonPropertyName("clientId")]
    public string ClientId { get; init; } = SentinelClientId;

    [JsonPropertyName("statusTrackingWatermark")]
    public required DateTime StatusTrackingWatermark { get; init; }

    [JsonPropertyName("mappingWatermark")]
    public required DateTime MappingWatermark { get; init; }

    [JsonPropertyName("lastSuccessfulRunUtc")]
    public required DateTime LastSuccessfulRunUtc { get; init; }
}
