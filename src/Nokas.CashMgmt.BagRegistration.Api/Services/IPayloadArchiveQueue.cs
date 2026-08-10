namespace Nokas.CashMgmt.BagRegistration.Api.Services;

public sealed record PayloadArchiveItem(string BlobName, string Content, string CorrelationId);

public interface IPayloadArchiveQueue
{
    ValueTask EnqueueAsync(PayloadArchiveItem item);
    IAsyncEnumerable<PayloadArchiveItem> ReadAllAsync(CancellationToken cancellationToken);
}