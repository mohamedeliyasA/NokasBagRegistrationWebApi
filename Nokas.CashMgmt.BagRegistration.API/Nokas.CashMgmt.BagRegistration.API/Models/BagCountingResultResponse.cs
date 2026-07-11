namespace Nokas.CashMgmt.BagRegistration.API.Models
{
    public class BagCountingResultResponse
    {
        public string customerRefNumber { get; set; }
        public string BagNumber { get; set; }
        public string Description { get; set; }
        public string BagStatus { get; set; }   // ✅ renamed from CountingStatus
        public string BagLifecycleId { get; set; }
        public string RequestCorrelationId { get; set; }
        public decimal DeclaredAmount { get; set; }
        public decimal? ActualCountedAmount { get; set; }
        public decimal? DeviationAmount { get; set; }
        public string? CountedDate { get; set; }

        public static BagCountingResultResponse CreateNotFoundSummary(string bagNumber, string clientId, string requestCorrelationId)
        {
            return new BagCountingResultResponse
            {
                customerRefNumber = "NA",
                BagNumber = bagNumber,
                BagStatus = "not-found",
                Description = $"No bag found with the BagNumber: {bagNumber} for this client with Id: {clientId}.",
                BagLifecycleId = "NA",
                RequestCorrelationId = requestCorrelationId,
                DeclaredAmount = 0m
            };
        }
    }
}
