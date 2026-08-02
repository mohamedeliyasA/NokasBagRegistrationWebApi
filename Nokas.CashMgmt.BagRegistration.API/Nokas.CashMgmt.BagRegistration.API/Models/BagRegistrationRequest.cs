using System.Text.Json.Serialization;
using System.Xml.Serialization;
using Newtonsoft.Json;

namespace Nokas.CashMgmt.BagRegistration.API.Models
{
    [XmlRoot("BagRegistrationRequest")]
    public class BagRegistrationRequest
    {
        // Id is used internally.
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonPropertyName("bagRegistration")]
        [XmlElement("BagRegistration")]
        public BagRegistration BagRegistration { get; set; }

        [XmlElement("DownstreamSystemId")]
        public string DownstreamSystemId { get; set; }

        /// <summary>
        /// Used internally to track registration lifecycle. Not required from consumer.
        /// </summary>
        [XmlElement("RegistrationStatus")]
        public string RegistrationStatus { get; set; } = "Registered"; // Default to Registered

        [JsonProperty("clientId")]
        public string ClientId { get; set; }

        public string BagLifecycleId { get; set; }
        public string CorrelationId { get; set; }

        public List<OperationHistoryEntry> OperationHistory { get; set; } = new();
    }
}
