
using Newtonsoft.Json;

namespace Nokas.CashMgmt.BagRegistration.API.Models
{
    public class ClientDirectoryItem
    {
        [JsonProperty("clientId")]
        public string ClientId { get; set; }

        [JsonProperty("customerNumber")]
        public string CustomerNumber { get; set; }

        [JsonProperty("customerName")]
        public string CustomerName { get; set; }
    }

    public class ClientDirectoryDocument
    {
        [JsonProperty("id")]
        public string Id { get; set; } = "static-client-list";

        [JsonProperty("clients")]
        public List<ClientDirectoryItem> Clients { get; set; } = new();
    }

}
