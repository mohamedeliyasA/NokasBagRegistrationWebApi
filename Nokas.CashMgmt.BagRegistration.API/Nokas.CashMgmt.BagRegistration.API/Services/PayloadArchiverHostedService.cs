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
            while (!stoppingToken.IsCancellationRequested)
            {
                var item = await _queue.DequeueAsync(stoppingToken);
                await _archiveService.ArchivePayloadAsync(item.BlobName, item.Content);
            }
        }
    }
}
