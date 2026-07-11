namespace Nokas.CashMgmt.BagRegistration.API.Models
{
    public class BagRegResponse
    {
        public string customerRefNumber { get; set; }
        public string BagNumber { get; set; }
        public string Description { get; set; }
        public string RegistrationStatus { get; set; }
        public string BagLifecycleId { get; set; }
        public string RequestCorrelationId { get; set; }

        public static BagRegResponse CreateNotFoundSummary(string bagNumber, string clientId, string requestCorrelationId)
        {
            return new BagRegResponse
            {
                customerRefNumber = "NA",
                BagNumber = "NA",
                RegistrationStatus = "NA",
                Description = $"No bag found with the BagNumber: {bagNumber} for this client with Id: {clientId}.",
                BagLifecycleId = "NA",
                RequestCorrelationId = requestCorrelationId
            };
        }
    }



}
