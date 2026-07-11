using Nokas.CashMgmt.BagRegistration.API.Models;

namespace Nokas.CashMgmt.BagRegistration.API.Repository
{
    public interface IBagRegistrationRepo
    {
        Task<BagRegistrationRequest?> GetBagByNumberForClientAsync(string bagNumber, string clientId);

        Task<BagRegistrationRequest> CreateBagRegistration(BagRegistrationRequest bagRegistrationRequest);

        Task<BagRegistrationRequest> UpdateBagRegistration(BagRegistrationRequest bagUpdate);

        Task EnsureClientInDirectoryAsync(string clientId, string customerNumber, string customerName);
       
    }
}
