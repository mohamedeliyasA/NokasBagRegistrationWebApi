using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace Nokas.CashMgmt.No.Api.Models;

[XmlRoot("BagRegistrationRequest")]
public sealed class BagRegistrationRequestDto
{
    [Required]
    [JsonPropertyName("bagRegistration")]
    [XmlElement("BagRegistration")]
    public required BagRegistrationDto BagRegistration { get; init; }
}

public sealed class BagRegistrationDto : IValidatableObject
{
    [Required]
    [JsonPropertyName("customerRefNumber")]
    [XmlElement("CustomerRefNumber")]
    public required string CustomerRefNumber { get; init; }

    [Required]
    [RegularExpression("^(NOK|Valuta)$", ErrorMessage = "registrationType must be NOK or Valuta.")]
    [JsonPropertyName("registrationType")]
    [XmlElement("RegistrationType")]
    public required string RegistrationType { get; init; }

    [Required]
    [JsonPropertyName("messageId")]
    [XmlElement("MessageId")]
    public required string MessageId { get; init; }

    [Required]
    [JsonPropertyName("bagNumber")]
    [XmlElement("BagNumber")]
    public required string BagNumber { get; init; }

    [JsonPropertyName("registrationDate")]
    [XmlElement("RegistrationDate")]
    public string? RegistrationDate { get; init; }

    [JsonPropertyName("turnOverDate")]
    [XmlElement("TurnOverDate")]
    public string? TurnOverDate { get; init; }

    [JsonPropertyName("registeredNotes")]
    [XmlElement("RegisteredNotes")]
    public string? RegisteredNotes { get; init; }

    [JsonPropertyName("registeredCoins")]
    [XmlElement("RegisteredCoins")]
    public string? RegisteredCoins { get; init; }

    [JsonPropertyName("registeredChecks")]
    [XmlElement("RegisteredChecks")]
    public string? RegisteredChecks { get; init; }

    [JsonPropertyName("totalOrderAmount")]
    [XmlElement("TotalOrderAmount")]
    public string? TotalOrderAmount { get; init; }

    [JsonPropertyName("bagItemsLC")]
    [XmlElement("BagItemsLC")]
    public BagItemsLcDto? BagItemsLC { get; init; }

    // Reserved for a future currency phase; rejected by validation until then.
    [JsonPropertyName("bagItemsFC")]
    [XmlArray("BagItemsFC")]
    [XmlArrayItem("BagItemFC")]
    public List<BagItemFcDto>? BagItemsFC { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.Equals(RegistrationType, "Valuta", StringComparison.OrdinalIgnoreCase))
        {
            yield return new ValidationResult(
                "registrationType 'Valuta' is not supported by this API. Only NOK is supported.",
                [nameof(RegistrationType)]);

            yield break;
        }

        // registrationType is NOK from here on.
        if (string.IsNullOrWhiteSpace(TotalOrderAmount))
        {
            yield return new ValidationResult(
                "totalOrderAmount is required when registrationType is NOK.",
                [nameof(TotalOrderAmount)]);
        }

        if (BagItemsFC is { Count: > 0 })
        {
            yield return new ValidationResult(
                "bagItemsFC must be empty when registrationType is NOK.",
                [nameof(BagItemsFC)]);
        }
    }
}

public sealed class BagItemsLcDto
{
    [JsonPropertyName("seddel1000")]
    [XmlElement("Seddel1000")]
    public string? Seddel1000 { get; init; }

    [JsonPropertyName("seddel500")]
    [XmlElement("Seddel500")]
    public string? Seddel500 { get; init; }

    [JsonPropertyName("seddel200")]
    [XmlElement("Seddel200")]
    public string? Seddel200 { get; init; }

    [JsonPropertyName("seddel100")]
    [XmlElement("Seddel100")]
    public string? Seddel100 { get; init; }

    [JsonPropertyName("seddel50")]
    [XmlElement("Seddel50")]
    public string? Seddel50 { get; init; }
}

public sealed class BagItemFcDto
{
    [JsonPropertyName("itemCurrencyCode")]
    [XmlElement("ItemCurrencyCode")]
    public string? ItemCurrencyCode { get; init; }

    [JsonPropertyName("items")]
    [XmlArray("Items")]
    [XmlArrayItem("Item")]
    public List<ForeignCurrencyItemDto>? Items { get; init; }
}

public sealed class ForeignCurrencyItemDto
{
    [JsonPropertyName("Denomination")]
    [XmlElement("Denomination")]
    public string? Denomination { get; init; }

    [JsonPropertyName("CurrencyAmount")]
    [XmlElement("CurrencyAmount")]
    public string? CurrencyAmount { get; init; }
}