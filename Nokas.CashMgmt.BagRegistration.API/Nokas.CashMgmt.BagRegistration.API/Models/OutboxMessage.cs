using Newtonsoft.Json;

namespace Nokas.CashMgmt.BagRegistration.API.Models
{
    public class OutboxMessage
    {
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonProperty("clientId")]
        public string ClientId { get; set; }

        public string DocType { get; set; } = "outbox";

        public string Subject { get; set; }

        public string Payload { get; set; }

        public string Status { get; set; } = "Pending";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Cosmos's reserved expiry field (seconds). Only set once Published, so a dispatcher
        // hiccup can never cause a not-yet-sent event to expire before it's actually delivered.
        [JsonProperty("ttl")]
        public int? Ttl { get; set; }

        // The bagreg container has a unique-key policy on /BagRegistration/BagNumber. A document
        // missing that path entirely is treated by Cosmos as sharing one "undefined" value per
        // partition, so a second outbox record in the same partition would collide with the first
        // and fail the whole transactional batch. This placeholder gives every outbox document its
        // own distinct value at that exact path, using a prefix that can never match a real bag number.
        public OutboxBagNumberPlaceholder BagRegistration { get; set; }

        public OutboxMessage()
        {
            BagRegistration = new OutboxBagNumberPlaceholder { BagNumber = $"__outbox_{Id}" };
        }
    }

    public class OutboxBagNumberPlaceholder
    {
        public string BagNumber { get; set; }
    }
}
