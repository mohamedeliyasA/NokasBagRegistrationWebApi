using System.Text.Json.Serialization;
using Nokas.CashMgmt.BagRegistration.Api.Models;

namespace Nokas.CashMgmt.BagRegistration.Api.Domain;

// Wire shape for the "bagregistration" Service Bus message, frozen by BizTalk/BagRegistration.xsd.
// actionFlag and registeredBy are mandatory in that schema and are server-computed, not
// partner-supplied, so this is built explicitly rather than re-serializing the request DTO.
public sealed class BagRegistrationOutboxPayload
{
    [JsonPropertyName("bagRegistration")]
    public required BagRegistrationOutboxDetail BagRegistration { get; init; }

    public static BagRegistrationOutboxPayload From(BagRegistrationDto request, string registeredBy) => new()
    {
        BagRegistration = new BagRegistrationOutboxDetail
        {
            CustomerRefNumber = request.CustomerRefNumber,
            RegistrationType = request.RegistrationType,
            MessageId = request.MessageId,
            BagNumber = request.BagNumber,
            ActionFlag = "Create",
            RegisteredBy = registeredBy,
            RegistrationDate = request.RegistrationDate,
            TurnOverDate = request.TurnOverDate,
            RegisteredNotes = request.RegisteredNotes,
            RegisteredCoins = request.RegisteredCoins,
            RegisteredChecks = request.RegisteredChecks,
            TotalOrderAmount = request.TotalOrderAmount,
            BagItemsLC = request.BagItemsLC,
            BagItemsFC = request.BagItemsFC
        }
    };
}

public sealed class BagRegistrationOutboxDetail
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
    public required string ActionFlag { get; init; }

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

    [JsonPropertyName("bagItemsLC")]
    public BagItemsLcDto? BagItemsLC { get; init; }

    [JsonPropertyName("bagItemsFC")]
    public List<BagItemFcDto>? BagItemsFC { get; init; }
}
