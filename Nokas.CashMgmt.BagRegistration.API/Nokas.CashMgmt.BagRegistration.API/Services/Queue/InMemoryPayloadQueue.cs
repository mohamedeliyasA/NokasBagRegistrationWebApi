using System.Threading.Channels;
using Nokas.CashMgmt.BagRegistration.API.Models;

namespace Nokas.CashMgmt.BagRegistration.API.Services.Queue
{
    public class InMemoryPayloadQueue : IPayloadQueue
    {
        private readonly Channel<PayloadBlobItem> _channel = Channel.CreateUnbounded<PayloadBlobItem>();

        public Task EnqueueAsync(PayloadBlobItem item) => _channel.Writer.WriteAsync(item).AsTask();
        public Task<PayloadBlobItem> DequeueAsync(CancellationToken token) => _channel.Reader.ReadAsync(token).AsTask();

    }
}
