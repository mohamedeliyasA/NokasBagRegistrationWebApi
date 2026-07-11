using Nokas.CashMgmt.BagRegistration.API.Models;

namespace Nokas.CashMgmt.BagRegistration.API.Services.Queue
{
    public interface IPayloadQueue
    {
        Task EnqueueAsync(PayloadBlobItem item);
        Task<PayloadBlobItem> DequeueAsync(CancellationToken cancellationToken);
    }
}
