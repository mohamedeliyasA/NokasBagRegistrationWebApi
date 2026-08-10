using System.Text.Json.Serialization;

namespace Nokas.CashMgmt.BagRegistration.Api.Documents;

public sealed class OutboxMessageDocument
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = Guid.NewGuid().ToString();

    [JsonPropertyName("clientId")]
    public required string ClientId { get; set; }

    [JsonPropertyName("subject")]
    public required string Subject { get; init; }

    [JsonPropertyName("payload")]
    public required string Payload { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = "Pending";

    [JsonPropertyName("docType")]
    public string DocType { get; init; } = "outbox";

    // Reserved Cosmos property. Must be omitted (not null) until published —
    // Cosmos rejects a present-but-null ttl with 400.
    [JsonPropertyName("ttl")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Ttl { get; set; }

    // Unique-key placeholder for /bagRegistration/bagNumber. Must be unique per
    // message — Cosmos treats a missing or shared value as one "undefined" key
    // per partition, which collides across messages.
    [JsonPropertyName("bagRegistration")]
    public object BagRegistration { get; init; } = new { BagNumber = Guid.NewGuid().ToString() };
}