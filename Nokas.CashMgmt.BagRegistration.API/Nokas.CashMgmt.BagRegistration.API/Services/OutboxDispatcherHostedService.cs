using Nokas.CashMgmt.BagRegistration.API.Repository;

namespace Nokas.CashMgmt.BagRegistration.API.Services
{
    public class OutboxDispatcherHostedService : BackgroundService
    {
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
        private const int BatchSize = 50;

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IServiceBusSender _serviceBusSender;
        private readonly ILogger<OutboxDispatcherHostedService> _logger;

        public OutboxDispatcherHostedService(
            IServiceScopeFactory scopeFactory,
            IServiceBusSender serviceBusSender,
            ILogger<OutboxDispatcherHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _serviceBusSender = serviceBusSender;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(PollInterval);

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var repo = scope.ServiceProvider.GetRequiredService<IBagRegistrationRepo>();

                    var pending = await repo.GetPendingOutboxMessagesAsync(BatchSize, stoppingToken);

                    foreach (var message in pending)
                    {
                        try
                        {
                            await _serviceBusSender.SendMessageAsync(message.Payload, message.Subject, message.Id, stoppingToken);
                            await repo.MarkOutboxMessagePublishedAsync(message.Id, message.ClientId, stoppingToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogCritical(ex, "Failed to dispatch outbox message {OutboxId} for client {ClientId}. Left pending for retry.", message.Id, message.ClientId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogCritical(ex, "Outbox dispatch cycle failed.");
                }

            }
        }
    }
}
