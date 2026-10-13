using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace Nokas.CashMgmt.BagRegistration.Api.Models;

[XmlRoot("CountingResultItem")]
public sealed class CountingResultItemDto
{
    [JsonPropertyName("bagNumber")]
    [XmlElement("BagNumber")]
    public required string BagNumber { get; init; }

    [JsonPropertyName("customerRefNumber")]
    [XmlElement("CustomerRefNumber")]
    public required string CustomerRefNumber { get; init; }

    [JsonPropertyName("status")]
    [XmlElement("Status")]
    public required string Status { get; init; }

    [JsonPropertyName("currencyType")]
    [XmlElement("CurrencyType")]
    public required string CurrencyType { get; init; }

    [JsonPropertyName("declaredAmount")]
    [XmlElement("DeclaredAmount")]
    public decimal? DeclaredAmount { get; init; }

    [JsonPropertyName("countedAmount")]
    [XmlElement("CountedAmount")]
    public decimal? CountedAmount { get; init; }

    [JsonPropertyName("deviationAmount")]
    [XmlElement("DeviationAmount")]
    public decimal? DeviationAmount { get; init; }

    [JsonPropertyName("registeredAt")]
    [XmlElement("RegisteredAt")]
    public DateTime? RegisteredAt { get; init; }

    [JsonPropertyName("countedAt")]
    [XmlElement("CountedAt")]
    public DateTime? CountedAt { get; init; }

    [JsonPropertyName("lastUpdatedAt")]
    [XmlElement("LastUpdatedAt")]
    public required DateTime LastUpdatedAt { get; init; }

    [JsonPropertyName("statusHistory")]
    [XmlElement("StatusHistory")]
    public List<CountingResultStatusHistoryItemDto> StatusHistory { get; init; } = [];
}

[XmlRoot("StatusHistoryItem")]
public sealed class CountingResultStatusHistoryItemDto
{
    [JsonPropertyName("status")]
    [XmlElement("Status")]
    public required string Status { get; init; }

    [JsonPropertyName("changedAt")]
    [XmlElement("ChangedAt")]
    public required DateTime ChangedAt { get; init; }
}

[XmlRoot("PaginationInfo")]
public sealed class PaginationDto
{
    [JsonPropertyName("pageSize")]
    [XmlElement("PageSize")]
    public required int PageSize { get; init; }

    [JsonPropertyName("count")]
    [XmlElement("Count")]
    public required int Count { get; init; }

    [JsonPropertyName("hasMore")]
    [XmlElement("HasMore")]
    public required bool HasMore { get; init; }

    [JsonPropertyName("continuationToken")]
    [XmlElement("ContinuationToken")]
    public string? ContinuationToken { get; init; }
}

[XmlRoot("CountingResultsResponse")]
public sealed class CountingResultsResponseDto
{
    [JsonPropertyName("items")]
    [XmlElement("Items")]
    public required List<CountingResultItemDto> Items { get; init; }

    [JsonPropertyName("pagination")]
    [XmlElement("Pagination")]
    public required PaginationDto Pagination { get; init; }

    [JsonPropertyName("dataAsOf")]
    [XmlElement("DataAsOf")]
    public DateTime? DataAsOf { get; init; }

    [JsonPropertyName("correlationId")]
    [XmlElement("CorrelationId")]
    public required string CorrelationId { get; init; }
}
