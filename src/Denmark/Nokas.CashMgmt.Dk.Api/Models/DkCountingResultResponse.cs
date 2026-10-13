using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace Nokas.CashMgmt.Dk.Api.Models;

[XmlRoot("CountingResultItem")]
public sealed class DkCountingResultItemDto
{
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    [JsonPropertyName("bagNumber")]
    [XmlElement("BagNumber", IsNullable = true)]
    public string? BagNumber { get; init; }

    [JsonPropertyName("customerRefNumber")]
    [XmlElement("CustomerRefNumber")]
    public required string CustomerRefNumber { get; init; }

    /// <summary>"registered", "counted", or "unknown" (a ServiceNow state not yet mapped).</summary>
    [JsonPropertyName("status")]
    [XmlElement("Status")]
    public required string Status { get; init; }

    [JsonPropertyName("currencyType")]
    [XmlElement("CurrencyType")]
    public required string CurrencyType { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    [JsonPropertyName("declaredAmount")]
    [XmlElement("DeclaredAmount", IsNullable = true)]
    public decimal? DeclaredAmount { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    [JsonPropertyName("countedAmount")]
    [XmlElement("CountedAmount", IsNullable = true)]
    public decimal? CountedAmount { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    [JsonPropertyName("deviationAmount")]
    [XmlElement("DeviationAmount", IsNullable = true)]
    public decimal? DeviationAmount { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    [JsonPropertyName("registeredAt")]
    [XmlElement("RegisteredAt", IsNullable = true)]
    public DateTime? RegisteredAt { get; init; }

    /// <summary>The day the bag was counted. Null until counted.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    [JsonPropertyName("countedAt")]
    [XmlElement("CountedAt", IsNullable = true)]
    public DateTime? CountedAt { get; init; }

    [JsonPropertyName("lastUpdatedAt")]
    [XmlElement("LastUpdatedAt")]
    public required DateTime LastUpdatedAt { get; init; }

    /// <summary>ServiceNow's own record identifier. Stable per record, unlike bagNumber, which is reused - quote it in support requests.</summary>
    [JsonPropertyName("recordId")]
    [XmlElement("RecordId")]
    public required string RecordId { get; init; }
}

[XmlRoot("PaginationInfo")]
public sealed class DkPaginationDto
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

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    [JsonPropertyName("continuationToken")]
    [XmlElement("ContinuationToken", IsNullable = true)]
    public string? ContinuationToken { get; init; }
}

[XmlRoot("CountingResultsResponse")]
public sealed class DkCountingResultsResponseDto
{
    [JsonPropertyName("items")]
    [XmlElement("Items")]
    public required List<DkCountingResultItemDto> Items { get; init; }

    [JsonPropertyName("pagination")]
    [XmlElement("Pagination")]
    public required DkPaginationDto Pagination { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    [JsonPropertyName("dataAsOf")]
    [XmlElement("DataAsOf", IsNullable = true)]
    public DateTime? DataAsOf { get; init; }

    [JsonPropertyName("correlationId")]
    [XmlElement("CorrelationId")]
    public required string CorrelationId { get; init; }
}
