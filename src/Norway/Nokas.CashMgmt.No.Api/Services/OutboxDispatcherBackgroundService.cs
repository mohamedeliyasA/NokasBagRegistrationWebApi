using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using Nokas.CashMgmt.No.Api.Configuration;
using Nokas.CashMgmt.No.Api.Documents;

namespace Nokas.CashMgmt.No.Api.Services;

public sealed class OutboxDispatcherBackgroundService(
    IServiceScopeFactory scopeFactory,
    ServiceBusClient serviceBusClient,
    IOptions<ServiceBusOptions> serviceBusOptions,
    IOptions<CosmosDbOptions> cosmosOptions,
    ILogger<OutboxDispatcherBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private const int BatchSize = 50;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var sender = serviceBusClient.CreateSender(serviceBusOptions.Value.TopicName);
        using var timer = new PeriodicTimer(PollInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var cosmosClient = scope.ServiceProvider.GetRequiredService<CosmosClient>();
                var container = cosmosClient.GetContainer(cosmosOptions.Value.DatabaseName, cosmosOptions.Value.BagRegistrationContainerName);

                var query = new QueryDefinition("SELECT TOP @maxItems * FROM c WHERE c.docType = 'outbox' AND c.status = 'Pending'")
                    .WithParameter("@maxItems", BatchSize);

                using var iterator = container.GetItemQueryIterator<OutboxMessageDocument>(query);

                while (iterator.HasMoreResults)
                {
                    foreach (var message in await iterator.ReadNextAsync(stoppingToken))
                    {
                        try
                        {
                            await sender.SendMessageAsync(
                                new ServiceBusMessage(message.Payload) { Subject = message.Subject, MessageId = message.Id },
                                stoppingToken);

                            await container.PatchItemAsync<OutboxMessageDocument>(
                                message.Id,
                                new PartitionKey(message.ClientId),
                                [
                                    PatchOperation.Set("/status", "Published"),
                                    PatchOperation.Set("/ttl", (int)TimeSpan.FromDays(2).TotalSeconds)
                                ],
                                cancellationToken: stoppingToken);
                        }
                        catch (Exception ex)
                        {
                            logger.LogCritical(ex, "Failed to dispatch outbox message {OutboxId} for client {ClientId}. Left pending for retry.",
                                message.Id, message.ClientId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "Outbox dispatch cycle failed.");
            }
        }
    }
}