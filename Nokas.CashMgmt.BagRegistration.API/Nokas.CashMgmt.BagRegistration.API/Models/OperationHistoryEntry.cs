namespace Nokas.CashMgmt.BagRegistration.API.Models
{
    public class OperationHistoryEntry
    {
        public string Action { get; set; } // "Create", "Update", "Delete"
        public DateTime Timestamp { get; set; }
        public string BagLifeCycleId { get; set; }
        public string PerformedBy { get; set; } // e.g., client app ID from JWT

        public string RequestCorrelationId { get; set; }
        public string? archiveBlobName { get; set; }
    }
}
