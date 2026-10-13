using System.Text.Json.Serialization;

namespace Nokas.CashMgmt.Dk.BagRegistration.Api.Documents;

/// <summary>
/// Maps an onboarded partner's client id to a human-readable customer name. This is the only
/// place that mapping lives - it exists so a request forwarded to ServiceNow (or any future
/// backend) always carries a readable customer identity, without ServiceNow needing to
/// maintain its own partner directory or NOKAS needing a new field/endpoint per customer.
/// </summary>
public sealed class DkCustomerDirectoryDocument
{
    /// <summary>Equal to ClientId. Point-read by id, no query needed.</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("clientId")]
    public required string ClientId { get; init; }

    [JsonPropertyName("customerName")]
    public required string CustomerName { get; init; }

    [JsonPropertyName("active")]
    public bool Active { get; init; } = true;

    [JsonPropertyName("createdAtUtc")]
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;

    [JsonPropertyName("docType")]
    public string DocType { get; init; } = "dkCustomerDirectory";
}
