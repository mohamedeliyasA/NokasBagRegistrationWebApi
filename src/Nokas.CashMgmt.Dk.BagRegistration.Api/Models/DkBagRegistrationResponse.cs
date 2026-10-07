using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace Nokas.CashMgmt.Dk.BagRegistration.Api.Models;

/// <summary>
/// The bag registration response, shared by POST and GET exactly as the Norwegian API does.
///
/// This is the real contract, not a stub-shaped one: when the Danish backend goes live the
/// only thing that changes is the text in <see cref="Description"/>. Nothing here is
/// conditional on the endpoint being a stand-in, so partner code written today keeps working.
/// </summary>
[XmlRoot("BagRegResponse")]
public sealed class DkBagRegistrationResponse
{
    [JsonPropertyName("customerRefNumber")]
    [XmlElement("CustomerRefNumber")]
    public required string CustomerRefNumber { get; init; }

    [JsonPropertyName("bagNumber")]
    [XmlElement("BagNumber")]
    public required string BagNumber { get; init; }

    // Echoed from the request. Denmark makes messageId mandatory, so returning it lets a
    // partner match a response to the message they sent without correlating on bag number.
    [JsonPropertyName("messageId")]
    [XmlElement("MessageId")]
    public required string MessageId { get; init; }

    /// <summary>
    /// Human-readable outcome. Carries the MOCK warning in capitals while this deployment is
    /// a stand-in; becomes an ordinary success message once the backend is live.
    /// </summary>
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

    // Present only when the payload was accepted but looks questionable, e.g. a
    // totalOrderAmount that does not match the denomination breakdown.
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    [JsonPropertyName("warnings")]
    [XmlArray("Warnings")]
    [XmlArrayItem("Warning")]
    public string[]? Warnings { get; init; }
}
