using Nokas.CashMgmt.BagRegistration.API.Models;
using Nokas.CashMgmt.BagRegistration.API.Services.Queue;

namespace Nokas.CashMgmt.BagRegistration.API.Services
{
    public class PayloadArchiverHostedService : BackgroundService
    {
        private readonly IPayloadQueue _queue;
        private readonly IBlobArchiveService _archiveService;
        private readonly ILogger<PayloadArchiverHostedService> _logger;

        public PayloadArchiverHostedService(
            IPayloadQueue queue,
            IBlobArchiveService archiveService,
            ILogger<PayloadArchiverHostedService> logger)
        {
            _queue = queue;
            _archiveService = archiveService;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await _archiveService.EnsureContainerExistsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to ensure the archive container exists. Will retry on first upload.");
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                PayloadBlobItem item;
                try
                {
                    item = await _queue.DequeueAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    await _archiveService.ArchivePayloadAsync(item.BlobName, item.Content, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to archive payload {BlobName}. Continuing to process the queue.", item.BlobName);
                }
            }
        }
    }
}
