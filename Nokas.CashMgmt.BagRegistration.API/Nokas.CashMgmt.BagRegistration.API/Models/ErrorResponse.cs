namespace Nokas.CashMgmt.BagRegistration.API.Models
{
    public class ErrorResponse
    {
        public string Title { get; set; }
        public int Status { get; set; }
        public string CorrelationId { get; set; }
        public string? Controller { get; set; }
        public string? Action { get; set; }
        public string? Path { get; set; }
        public string? Exception { get; set; }
        public string? StackTrace { get; set; }

        // Optional machine-readable fields
        public string? Field { get; set; }   // e.g., BagNumber
        public string? Code { get; set; }    // e.g., ERR_DUPLICATE_BAG
    }
}
