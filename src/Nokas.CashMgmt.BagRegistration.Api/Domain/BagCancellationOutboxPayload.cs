using System.Text.Json.Serialization;
using Nokas.CashMgmt.Shared.Contracts;

namespace Nokas.CashMgmt.BagRegistration.Api.Domain;

// Wire shape for the "deleteBagRegistration" Service Bus message - no XSD exists for this
// subject, so the old (currently-live) controller's exact field set/order is the contract.
// Serialize with DefaultIgnoreCondition = WhenWritingNull to match old behavior (nulls omitted).
public sealed class BagCancellationOutboxPayload
{
    [JsonPropertyName("cancelBagRegistration")]
    public required BagCancellationOutboxDetail CancelBagRegistration { get; init; }

    public static BagCancellationOutboxPayload From(
        string customerRefNumber, string bagNumber, string registeredBy, DateTime cancellationDateUtc, BagStatusRecord status) => new()
    {
        CancelBagRegistration = new BagCancellationOutboxDetail
        {
            CustomerRefNumber = customerRefNumber,
            BagNumber = bagNumber,
            ActionFlag = "cancel",
            RegisteredBy = registeredBy,
            CancellationDate = cancellationDateUtc.ToString("yyyy-MM-dd"),
            IntInnkastnr = status.intInnkastnr,
            VchCustomerId = status.vchCustomer_id,
            VchName = status.vchName,
            DblAmount = status.dblAmount,
            VchLocation = status.vchLocation,
            DtmRegDate = status.dtmRegDate,
            IntStatus = status.intStatus
        }
    };
}

public sealed class BagCancellationOutboxDetail
{
    [JsonPropertyName("customerRefNumber")]
    public required string CustomerRefNumber { get; init; }

    [JsonPropertyName("bagNumber")]
    public required string BagNumber { get; init; }

    [JsonPropertyName("actionFlag")]
    public required string ActionFlag { get; init; }

    [JsonPropertyName("registeredBy")]
    public required string RegisteredBy { get; init; }

    [JsonPropertyName("cancellationDate")]
    public required string CancellationDate { get; init; }

    [JsonPropertyName("intInnkastnr")]
    public string? IntInnkastnr { get; init; }

    [JsonPropertyName("vchCustomer_id")]
    public string? VchCustomerId { get; init; }

    [JsonPropertyName("vchName")]
    public string? VchName { get; init; }

    [JsonPropertyName("dblAmount")]
    public string? DblAmount { get; init; }

    [JsonPropertyName("vchLocation")]
    public string? VchLocation { get; init; }

    [JsonPropertyName("dtmRegDate")]
    public DateTime? DtmRegDate { get; init; }

    [JsonPropertyName("intStatus")]
    public int? IntStatus { get; init; }
}
