using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nokas.CashMgmt.Dk.Api.Documents;

// Journal of what the partner posted and what ServiceNow answered, written AFTER the call and
// never in the critical path. Not a source of truth - ServiceNow is. Failed and rejected
// attempts are kept deliberately for support. The payload is nested under its own property so that partner field names can
// never collide with the Cosmos system properties (id, ttl, _etag) at the document root.
public sealed class DkBagRegistrationDocument
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("clientId")]
    public required string ClientId { get; init; }

    // Same value as Id. Held separately so the document mirrors the Norwegian registration
    // document, where the id is the bag number and the lifecycle id is its own field.
    [JsonPropertyName("bagLifecycleId")]
    public required string BagLifecycleId { get; init; }

    [JsonPropertyName("correlationId")]
    public required string CorrelationId { get; init; }

    [JsonPropertyName("receivedAtUtc")]
    public required DateTime ReceivedAtUtc { get; init; }

    [JsonPropertyName("market")]
    public string Market { get; init; } = "DK";

    [JsonPropertyName("currencyType")]
    public string CurrencyType { get; init; } = "DKK";

    [JsonPropertyName("docType")]
    public string DocType { get; init; } = "dkBagRegistrationStub";

    [JsonPropertyName("accepted")]
    public required bool Accepted { get; init; }

    [JsonPropertyName("validationErrors")]
    public IReadOnlyList<DocumentValidationError>? ValidationErrors { get; init; }

    [JsonPropertyName("validationWarnings")]
    public IReadOnlyList<string>? ValidationWarnings { get; init; }

    // Lifted out of the payload once validated, so the stored documents are queryable and
    // browsable in Data Explorer. Null on a rejected payload.
    [JsonPropertyName("customerRefNumber")]
    public string? CustomerRefNumber { get; init; }

    [JsonPropertyName("cashRegisterId")]
    public string? CashRegisterId { get; init; }

    [JsonPropertyName("messageId")]
    public string? MessageId { get; init; }

    [JsonPropertyName("bagNumber")]
    public string? BagNumber { get; init; }

    [JsonPropertyName("registrationDate")]
    public string? RegistrationDate { get; init; }

    [JsonPropertyName("totalOrderAmount")]
    public decimal? TotalOrderAmount { get; init; }

    [JsonPropertyName("denominationTotal")]
    public decimal? DenominationTotal { get; init; }

    [JsonPropertyName("contentType")]
    public string? ContentType { get; init; }

    [JsonPropertyName("payloadBytes")]
    public required int PayloadBytes { get; init; }

    [JsonPropertyName("payloadIsJson")]
    public required bool PayloadIsJson { get; init; }

    // Populated when the body parsed as JSON and fits within the configured size budget.
    [JsonPropertyName("payload")]
    public JsonElement? Payload { get; init; }

    // Populated instead of Payload when the body was not JSON, again subject to the budget.
    [JsonPropertyName("payloadText")]
    public string? PayloadText { get; init; }

    // Set when the body exceeded the budget and lives only in Blob.
    [JsonPropertyName("payloadOmittedReason")]
    public string? PayloadOmittedReason { get; init; }

    [JsonPropertyName("archiveBlobName")]
    public string? ArchiveBlobName { get; init; }

    /// <summary>Free-form partner metadata from the request body, e.g. their own store/company ids.</summary>
    [JsonPropertyName("metaData")]
    public IReadOnlyDictionary<string, string>? MetaData { get; init; }

    [JsonPropertyName("requestHeaders")]
    public Dictionary<string, string> RequestHeaders { get; init; } = [];

    // What ServiceNow answered when this registration was sent. Null when the call was never
    // made (invalid payload, or a kardex owned by another client).
    [JsonPropertyName("serviceNow")]
    public ServiceNowRegistrationRecord? ServiceNow { get; init; }
}

/// <summary>What ServiceNow said when this registration was sent to it.</summary>
public sealed record ServiceNowRegistrationRecord
{
    [JsonPropertyName("forwardedAtUtc")]
    public DateTime ForwardedAtUtc { get; init; }

    /// <summary>success / duplicate / rejected / backend-error / unavailable.</summary>
    [JsonPropertyName("outcome")]
    public string? Outcome { get; init; }

    [JsonPropertyName("httpStatus")]
    public int? HttpStatus { get; init; }

    /// <summary>ServiceNow's own record id or message id.</summary>
    [JsonPropertyName("sysId")]
    public string? SysId { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("durationMs")]
    public int? DurationMs { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }

    /// <summary>Names of the partner headers actually sent onward to ServiceNow.</summary>
    [JsonPropertyName("forwardedHeaders")]
    public IReadOnlyList<string>? ForwardedHeaders { get; init; }

    /// <summary>
    /// The customer name resolved from the directory and included in the "nokasSource" object
    /// sent to ServiceNow. Null means the client id was not yet in the directory - customerId
    /// was still sent, this is enrichment only.
    /// </summary>
    [JsonPropertyName("customerName")]
    public string? CustomerName { get; init; }

    /// <summary>Kept only when the response was not a recognisable success.</summary>
    [JsonPropertyName("rawBody")]
    public string? RawBody { get; init; }
}

public sealed record DocumentValidationError
{
    [JsonPropertyName("field")]
    public required string Field { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }
}
