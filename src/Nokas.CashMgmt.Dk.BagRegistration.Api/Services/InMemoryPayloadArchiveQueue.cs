using System.Threading.Channels;

namespace Nokas.CashMgmt.Dk.BagRegistration.Api.Services;

public sealed class InMemoryPayloadArchiveQueue : IPayloadArchiveQueue
{
    private readonly Channel<PayloadArchiveItem> _channel = Channel.CreateUnbounded<PayloadArchiveItem>();

    public ValueTask EnqueueAsync(PayloadArchiveItem item) => _channel.Writer.WriteAsync(item);

    public IAsyncEnumerable<PayloadArchiveItem> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
