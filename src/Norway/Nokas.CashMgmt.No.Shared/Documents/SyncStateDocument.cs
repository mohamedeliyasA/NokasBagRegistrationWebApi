using System.Text.Json.Serialization;

namespace Nokas.CashMgmt.No.Shared.Documents;

public sealed class SyncStateDocument
{
    public const string DocumentId = "sync-state";

    [JsonPropertyName("id")]
    public string Id { get; init; } = DocumentId;

    [JsonPropertyName("lastSuccessfulRunUtc")]
    public required DateTime LastSuccessfulRunUtc { get; init; }
}
