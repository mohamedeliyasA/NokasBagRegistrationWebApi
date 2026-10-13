using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;
using Nokas.CashMgmt.Dk.Api.Configuration;

namespace Nokas.CashMgmt.Dk.Api.Services;

public sealed class PayloadArchiverBackgroundService(
    IPayloadArchiveQueue queue,
    BlobServiceClient blobServiceClient,
    IOptions<BlobStorageOptions> options,
    ILogger<PayloadArchiverBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var container = blobServiceClient.GetBlobContainerClient(options.Value.ContainerName);

        await foreach (var item in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                var blob = container.GetBlobClient(item.BlobName);
                using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(item.Content));
                await blob.UploadAsync(stream, overwrite: true, stoppingToken);
            }
            catch (Exception ex)
            {
                // One failed upload must not stop the loop - keep archiving the rest.
                logger.LogCritical(ex, "CorrelationId: {CorrelationId} - Failed to archive payload {BlobName}.",
                    item.CorrelationId, item.BlobName);
            }
        }
    }
}
