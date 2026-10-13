using System.Text.Json.Serialization;

namespace Nokas.CashMgmt.Dk.Shared.Documents;

/// <summary>
/// One counting-result record synced from ServiceNow. PK <c>clientId</c> (resolved via the
/// kardex map, never trusted directly from ServiceNow's own data), <c>id</c> = ServiceNow's own
/// <c>number</c> field (e.g. "NOC0430326") - its stable business record identifier, not
/// <c>bagNumber</c>, which recirculates, matching the same rule the Norwegian counting-result
/// container already follows with <c>nattsafeRecordId</c>.
/// </summary>
public sealed class DkCountingResultDocument
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("clientId")]
    public required string ClientId { get; init; }

    [JsonPropertyName("customerRefNumber")]
    public required string CustomerRefNumber { get; init; }

    [JsonPropertyName("bagNumber")]
    public string? BagNumber { get; init; }

    [JsonPropertyName("serviceNowNumber")]
    public required string ServiceNowNumber { get; init; }

    /// <summary>Bucketed status - see DkCountingResultStatusMapper. "registered" | "counted" | "unknown".</summary>
    [JsonPropertyName("status")]
    public required string Status { get; init; }

    /// <summary>The raw ServiceNow "state" value, kept for forensics since the full vocabulary is unconfirmed.</summary>
    [JsonPropertyName("rawState")]
    public string? RawState { get; init; }

    [JsonPropertyName("currencyType")]
    public string CurrencyType { get; init; } = "DKK";

    [JsonPropertyName("declaredAmount")]
    public decimal? DeclaredAmount { get; init; }

    [JsonPropertyName("countedAmount")]
    public decimal? CountedAmount { get; init; }

    [JsonPropertyName("deviationAmount")]
    public decimal? DeviationAmount { get; init; }

    [JsonPropertyName("registeredAt")]
    public DateTime? RegisteredAt { get; init; }

    /// <summary>ServiceNow reports the count date as a date only (no time).</summary>
    [JsonPropertyName("countedAt")]
    public DateTime? CountedAt { get; init; }

    /// <summary>ServiceNow's last-modified time for the record.</summary>
    [JsonPropertyName("lastUpdatedAt")]
    public required DateTime LastUpdatedAt { get; init; }

    [JsonPropertyName("syncedAtUtc")]
    public DateTime SyncedAtUtc { get; init; } = DateTime.UtcNow;

    [JsonPropertyName("docType")]
    public string DocType { get; init; } = "dkCountingResult";
}
