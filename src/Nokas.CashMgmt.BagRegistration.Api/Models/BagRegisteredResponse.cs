using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace Nokas.CashMgmt.BagRegistration.Api.Models;

[XmlRoot("BagRegResponse")]
public sealed class BagRegisteredResponse
{
    [JsonPropertyName("customerRefNumber")]
    [XmlElement("CustomerRefNumber")]
    public required string CustomerRefNumber { get; init; }

    [JsonPropertyName("bagNumber")]
    [XmlElement("BagNumber")]
    public required string BagNumber { get; init; }

    [JsonPropertyName("description")]
    [XmlElement("Description")]
    public required string Description { get; init; }

    [JsonPropertyName("registrationStatus")]
    [XmlElement("RegistrationStatus")]
    public required string RegistrationStatus { get; init; }

    [JsonPropertyName("bagLifecycleId")]
    [XmlElement("BagLifecycleId")]
    public required string BagLifecycleId { get; init; }

    [JsonPropertyName("requestCorrelationId")]
    [XmlElement("RequestCorrelationId")]
    public required string RequestCorrelationId { get; init; }
}