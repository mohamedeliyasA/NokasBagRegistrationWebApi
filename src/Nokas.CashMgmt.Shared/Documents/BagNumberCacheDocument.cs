using System.Text.Json.Serialization;

namespace Nokas.CashMgmt.Shared.Documents;

public sealed class BagNumberCacheDocument
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("syncedAt")]
    public required DateTime SyncedAt { get; init; }
}
