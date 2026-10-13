using System.Text.Json.Serialization;

namespace Nokas.CashMgmt.No.Api.Documents;

public sealed class BagRegistrationDocument
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("clientId")]
    public required string ClientId { get; init; }

    [JsonPropertyName("bagLifecycleId")]
    public required string BagLifecycleId { get; init; }

    [JsonPropertyName("correlationId")]
    public required string CorrelationId { get; init; }

    [JsonPropertyName("registrationStatus")]
    public required string RegistrationStatus { get; set; }

    [JsonPropertyName("bagRegistration")]
    public required BagRegistrationDetail BagRegistration { get; init; }

    [JsonPropertyName("operationHistory")]
    public List<OperationHistoryEntry> OperationHistory { get; init; } = [];

    [JsonPropertyName("docType")]
    public string DocType { get; init; } = "bagRegistration";
}

public sealed class BagRegistrationDetail
{
    [JsonPropertyName("customerRefNumber")]
    public required string CustomerRefNumber { get; init; }

    [JsonPropertyName("registrationType")]
    public required string RegistrationType { get; init; }

    [JsonPropertyName("messageId")]
    public required string MessageId { get; init; }

    [JsonPropertyName("bagNumber")]
    public required string BagNumber { get; init; }

    [JsonPropertyName("actionFlag")]
    public string ActionFlag { get; set; } = "Create";

    [JsonPropertyName("registeredBy")]
    public required string RegisteredBy { get; init; }

    [JsonPropertyName("registrationDate")]
    public string? RegistrationDate { get; init; }

    [JsonPropertyName("turnOverDate")]
    public string? TurnOverDate { get; init; }

    [JsonPropertyName("registeredNotes")]
    public string? RegisteredNotes { get; init; }

    [JsonPropertyName("registeredCoins")]
    public string? RegisteredCoins { get; init; }

    [JsonPropertyName("registeredChecks")]
    public string? RegisteredChecks { get; init; }

    [JsonPropertyName("totalOrderAmount")]
    public string? TotalOrderAmount { get; init; }
}

public sealed record OperationHistoryEntry
{
    [JsonPropertyName("action")]
    public required string Action { get; init; }

    [JsonPropertyName("timestamp")]
    public required DateTime Timestamp { get; init; }

    [JsonPropertyName("bagLifecycleId")]
    public required string BagLifecycleId { get; init; }

    [JsonPropertyName("correlationId")]
    public required string CorrelationId { get; init; }

    [JsonPropertyName("performedBy")]
    public required string PerformedBy { get; init; }

    [JsonPropertyName("archiveBlobName")]
    public string? ArchiveBlobName { get; init; }
}
