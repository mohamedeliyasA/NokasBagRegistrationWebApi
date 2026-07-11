using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using System.Xml.Serialization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Swashbuckle.AspNetCore.Annotations;

namespace Nokas.CashMgmt.BagRegistration.API.Models
{
    public class BagRegistration : IValidatableObject
    {
        // mandatory, partner ref -> our branch id
        [Required]
        [JsonPropertyName("customerRefNumber")]
        [XmlElement("CustomerRefNumber")]
        public string CustomerRefNumber { get; set; }

        // mandatory; "NOK" (local) or "Valuta" (foreign)
        [Required]
        [RegularExpression("^(NOK|Valuta)$", ErrorMessage = "registrationType must be NOK or Valuta.")]
        [JsonPropertyName("registrationType")]
        [XmlElement("RegistrationType")]
        public string RegistrationType { get; set; }

        // GUID, mandatory (kept as string to avoid breaking callers)
        [Required]
        [JsonPropertyName("messageId")]
        [XmlElement("MessageId")]
        public string MessageId { get; set; }

        // length should match business rule (kept as string)
        [Required]
        [JsonPropertyName("bagNumber")]
        [XmlElement("BagNumber")]
        public string BagNumber { get; set; }
        
        [BindNever]        
        [ValidateNever]
        [JsonPropertyName("actionFlag")]
        [XmlElement("ActionFlag")]
     
        public string ActionFlag { get; set; } = "Create";

        [BindNever]       
        [ValidateNever]
        [JsonPropertyName("registeredBy")]
        [XmlElement("RegisteredBy")]

        public string RegisteredBy { get; set; }

        // optional; server can default to now if null/empty
        [JsonPropertyName("registrationDate")]
        [XmlElement("RegistrationDate")]
        public string? RegistrationDate { get; set; }

        // optional
        [JsonPropertyName("turnOverDate")]
        [XmlElement("TurnOverDate")]
        public string? TurnOverDate { get; set; }

        // optional amounts (kept as string to preserve current behavior)
        [JsonPropertyName("registeredNotes")]
        [XmlElement("RegisteredNotes")]
        public string? RegisteredNotes { get; set; }

        [JsonPropertyName("registeredCoins")]
        [XmlElement("RegisteredCoins")]
        public string? RegisteredCoins { get; set; }

        [JsonPropertyName("registeredChecks")]
        [XmlElement("RegisteredChecks")]
        public string? RegisteredChecks { get; set; }

        // mandatory only for NOK (nullable so it's not auto-required)
        [JsonPropertyName("totalOrderAmount")]
        [XmlElement("TotalOrderAmount")]
        public string? TotalOrderAmount { get; set; }

        // optional, applicable only to NOK
        [JsonPropertyName("bagItemsLC")]
        [XmlElement("BagItemsLC")]
        public BagItemsLC? BagItemsLC { get; set; }

        // optional, applicable only to Valuta (nullable so it's not auto-required)
        [JsonPropertyName("bagItemsFC")]
        [XmlArray("BagItemsFC")]
        [XmlArrayItem("BagItemFC")]
        public List<BagItemFC>? BagItemsFC { get; set; }

        // Conditional validation rules
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.Equals(RegistrationType, "NOK", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(TotalOrderAmount))
                {
                    yield return new ValidationResult(
                        "totalOrderAmount is required when registrationType is NOK.",
                        new[] { nameof(TotalOrderAmount) });
                }

                if (BagItemsFC is { Count: > 0 })
                {
                    yield return new ValidationResult(
                        "bagItemsFC must be empty when registrationType is NOK.",
                        new[] { nameof(BagItemsFC) });
                }
            }

            if (string.Equals(RegistrationType, "Valuta", StringComparison.OrdinalIgnoreCase))
            {
                if (BagItemsFC == null || BagItemsFC.Count == 0)
                {
                    yield return new ValidationResult(
                        "bagItemsFC must contain at least one item when registrationType is Valuta.",
                        new[] { nameof(BagItemsFC) });
                }

                if (BagItemsLC != null)
                {
                    yield return new ValidationResult(
                        "bagItemsLC must be omitted when registrationType is Valuta.",
                        new[] { nameof(BagItemsLC) });
                }
            }
        }
    }

    public class BagItemsLC
    {
        [JsonPropertyName("seddel1000")]
        [XmlElement("Seddel1000")]
        public string? Seddel1000 { get; set; }

        [JsonPropertyName("seddel500")]
        [XmlElement("Seddel500")]
        public string? Seddel500 { get; set; }

        [JsonPropertyName("seddel200")]
        [XmlElement("Seddel200")]
        public string? Seddel200 { get; set; }

        [JsonPropertyName("seddel100")]
        [XmlElement("Seddel100")]
        public string? Seddel100 { get; set; }

        [JsonPropertyName("seddel50")]
        [XmlElement("Seddel50")]
        public string? Seddel50 { get; set; }
    }


    public class BagItemFC
    {
        [JsonPropertyName("itemCurrencyCode")]
        [XmlElement("ItemCurrencyCode")]
        public string? ItemCurrencyCode { get; set; }

        [JsonPropertyName("items")]
        [XmlArray("Items")]
        [XmlArrayItem("Item")]
        public List<ForeignCurrencyItem>? Items { get; set; }
    }


    public class ForeignCurrencyItem
    {
        [JsonPropertyName("Denomination")]
        [XmlElement("Denomination")]
        public string? Denomination { get; set; }

        [JsonPropertyName("CurrencyAmount")]
        [XmlElement("CurrencyAmount")]
        public string? CurrencyAmount { get; set; }
    }


}
