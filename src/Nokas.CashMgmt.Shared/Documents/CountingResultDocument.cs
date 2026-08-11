using System.Text.Json.Serialization;

namespace Nokas.CashMgmt.Shared.Documents;

/// <summary>
/// Cosmos document for the `countingresults` container. PK /clientId, id = nattsafeRecordId
/// (Nattsafearkiv.intId) - never bagNumber, which recirculates.
/// </summary>
public sealed class CountingResultDocument
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("clientId")]
    public required string ClientId { get; init; }

    [JsonPropertyName("nattsafeRecordId")]
    public required int NattsafeRecordId { get; init; }

    [JsonPropertyName("bagNumber")]
    public required string BagNumber { get; init; }

    [JsonPropertyName("customerRefNumber")]
    public required string CustomerRefNumber { get; init; }

    [JsonPropertyName("customerNumber")]
    public required string CustomerNumber { get; init; }

    /// <summary>Granular NattsafeStatusMapper.ToLabel value (transport-fetch override already applied).
    /// The API's 6-value bucket filter folds over this at query time - see NattsafeStatusMapper.GranularLabelsForBucket.</summary>
    [JsonPropertyName("status")]
    public required string Status { get; init; }

    [JsonPropertyName("statusCode")]
    public required int StatusCode { get; init; }

    /// <summary>Constant "NOK" for now - phase 1 is NOK-only.</summary>
    [JsonPropertyName("currencyType")]
    public string CurrencyType { get; init; } = "NOK";

    [JsonPropertyName("declaredAmount")]
    public decimal? DeclaredAmount { get; init; }

    [JsonPropertyName("countedAmount")]
    public decimal? CountedAmount { get; init; }

    [JsonPropertyName("deviationAmount")]
    public decimal? DeviationAmount { get; init; }

    [JsonPropertyName("registeredAt")]
    public DateTime? RegisteredAt { get; init; }

    [JsonPropertyName("countedAt")]
    public DateTime? CountedAt { get; init; }

    [JsonPropertyName("transportFetchedAt")]
    public DateTime? TransportFetchedAt { get; init; }

    /// <summary>Max StatusTracking.dtmDate seen for this record in the sync run that wrote it
    /// (falls back to registeredAt when no tracking row exists) - never sync wall-clock.</summary>
    [JsonPropertyName("lastUpdatedAt")]
    public required DateTime LastUpdatedAt { get; init; }

    /// <summary>"API" when vchMyReference starts with "API_", else "Portal". Informational only,
    /// never used for authorization.</summary>
    [JsonPropertyName("registrationChannel")]
    public string? RegistrationChannel { get; init; }

    [JsonPropertyName("statusHistory")]
    public IReadOnlyList<CountingResultStatusHistoryEntry> StatusHistory { get; init; } = [];

    [JsonPropertyName("ttl")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Ttl { get; init; }
}

public sealed class CountingResultStatusHistoryEntry
{
    [JsonPropertyName("statusCode")]
    public required int StatusCode { get; init; }

    [JsonPropertyName("status")]
    public required string Status { get; init; }

    [JsonPropertyName("changedAt")]
    public required DateTime ChangedAt { get; init; }
}
