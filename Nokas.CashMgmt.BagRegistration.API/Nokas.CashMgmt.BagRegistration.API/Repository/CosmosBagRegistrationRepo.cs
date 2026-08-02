using System.Net;
using AutoMapper;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Nokas.CashMgmt.BagRegistration.API.Configuration;
using Nokas.CashMgmt.BagRegistration.API.Helpers;
using Nokas.CashMgmt.BagRegistration.API.Models;

namespace Nokas.CashMgmt.BagRegistration.API.Repository
{
    public class CosmosBagRegistrationRepo : IBagRegistrationRepo
    {
        private readonly Container _container;
        private readonly Container _clientDirectoryContainer;
        private readonly IMapper _mapper;
        private readonly ILogger<CosmosBagRegistrationRepo> _logger;

        public CosmosBagRegistrationRepo(CosmosClient cosmosClient,
                                         IOptions<CosmosDbOptions> options,
                                         IMapper mapper,
                                         ILogger<CosmosBagRegistrationRepo> logger)
        {
            var config = options.Value;
            _container = cosmosClient.GetContainer(config.DatabaseName, config.BagRegContainerName);
            _clientDirectoryContainer = cosmosClient.GetContainer(config.DatabaseName, config.ClientDirectoryContainerName);

            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<BagRegistrationRequest?> GetBagByNumberForClientAsync(string bagNumber, string clientId, CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _container.ReadItemAsync<BagRegistrationRequest>(
                    bagNumber,
                    new PartitionKey(clientId),
                    cancellationToken: cancellationToken);

                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex, "Cosmos read failed for BagNumber: {BagNumber}, ClientId: {ClientId}", bagNumber, clientId);
                throw;
            }
        }

        public async Task<BagRegistrationRequest> CreateBagRegistration(BagRegistrationRequest bagRegistrationRequest, OutboxMessage outboxMessage, CancellationToken cancellationToken = default)
        {
            if (bagRegistrationRequest == null)
                throw new ArgumentNullException(nameof(bagRegistrationRequest));
            if (outboxMessage == null)
                throw new ArgumentNullException(nameof(outboxMessage));

            var clientId = bagRegistrationRequest.ClientId;
            outboxMessage.ClientId = clientId;

            try
            {
                var batch = _container.CreateTransactionalBatch(new PartitionKey(clientId))
                    .CreateItem(bagRegistrationRequest)
                    .CreateItem(outboxMessage);

                using var response = await batch.ExecuteAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == HttpStatusCode.Conflict)
                    {
                        _logger.LogError("Bag number already exists: {BagNumber}", bagRegistrationRequest.BagRegistration?.BagNumber);
                        throw new DuplicateBagNumberException(bagRegistrationRequest.BagRegistration?.BagNumber ?? "Unknown");
                    }

                    _logger.LogError("Transactional batch failed with status {StatusCode} for BagNumber: {BagNumber}", response.StatusCode, bagRegistrationRequest.BagRegistration?.BagNumber);
                    throw new CosmosException(response.ErrorMessage, response.StatusCode, 0, response.ActivityId, response.RequestCharge);
                }

                return response.GetOperationResultAtIndex<BagRegistrationRequest>(0).Resource;
            }
            catch (DuplicateBagNumberException)
            {
                throw;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex, "Failed to create bag registration for BagNumber: {BagNumber}", bagRegistrationRequest.BagRegistration?.BagNumber);
                throw;
            }
        }

        public async Task<BagRegistrationRequest> UpdateBagRegistration(BagRegistrationRequest bagUpdate, CancellationToken cancellationToken = default)
        {
            if (bagUpdate == null) throw new ArgumentNullException(nameof(bagUpdate));

            var response = await _container.UpsertItemAsync(bagUpdate, new PartitionKey(bagUpdate.ClientId), cancellationToken: cancellationToken);
            return response.Resource;
        }

        public async Task UpdateBagRegistrationWithOutboxAsync(BagRegistrationRequest bagUpdate, OutboxMessage outboxMessage, CancellationToken cancellationToken = default)
        {
            if (bagUpdate == null) throw new ArgumentNullException(nameof(bagUpdate));
            if (outboxMessage == null) throw new ArgumentNullException(nameof(outboxMessage));

            var clientId = bagUpdate.ClientId;
            outboxMessage.ClientId = clientId;

            var batch = _container.CreateTransactionalBatch(new PartitionKey(clientId))
                .UpsertItem(bagUpdate)
                .CreateItem(outboxMessage);

            using var response = await batch.ExecuteAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Transactional batch failed with status {StatusCode} for BagNumber: {BagNumber}", response.StatusCode, bagUpdate.BagRegistration?.BagNumber);
                throw new CosmosException(response.ErrorMessage, response.StatusCode, 0, response.ActivityId, response.RequestCharge);
            }
        }

        public async Task<IReadOnlyList<OutboxMessage>> GetPendingOutboxMessagesAsync(int maxItems, CancellationToken cancellationToken = default)
        {
            var query = new QueryDefinition(
                "SELECT TOP @maxItems * FROM c WHERE c.DocType = 'outbox' AND c.Status = 'Pending'")
                .WithParameter("@maxItems", maxItems);

            var results = new List<OutboxMessage>();
            using var iterator = _container.GetItemQueryIterator<OutboxMessage>(query);
            while (iterator.HasMoreResults && results.Count < maxItems)
            {
                var page = await iterator.ReadNextAsync(cancellationToken);
                results.AddRange(page);
            }

            return results;
        }

        public async Task MarkOutboxMessagePublishedAsync(string outboxId, string clientId, CancellationToken cancellationToken = default)
        {
            var patch = new List<PatchOperation>
            {
                PatchOperation.Set("/Status", "Published"),
                PatchOperation.Set("/ttl", (int)TimeSpan.FromDays(2).TotalSeconds)
            };
            await _container.PatchItemAsync<OutboxMessage>(outboxId, new PartitionKey(clientId), patch, cancellationToken: cancellationToken);
        }

        public async Task EnsureClientInDirectoryAsync(string clientId, string customerNumber, string customerName)
        {
            const string docId = "static-client-list";
            var partitionKey = new PartitionKey(docId);

            try
            {
                var response = await _clientDirectoryContainer.ReadItemAsync<ClientDirectoryDocument>(docId, partitionKey);
                var document = response.Resource;

                if (!document.Clients.Any(c => c.ClientId == clientId))
                {
                    document.Clients.Add(new ClientDirectoryItem
                    {
                        ClientId = clientId,
                        CustomerNumber = customerNumber,
                        CustomerName = customerName
                    });

                    await _clientDirectoryContainer.ReplaceItemAsync(document, docId, partitionKey);
                }
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                var newDoc = new ClientDirectoryDocument
                {
                    Id = docId,
                    Clients = new List<ClientDirectoryItem>
                    {
                        new ClientDirectoryItem
                        {
                            ClientId = clientId,
                            CustomerNumber = customerNumber,
                            CustomerName = customerName
                        }
                    }
                };

                Console.WriteLine(JsonConvert.SerializeObject(newDoc));

                await _clientDirectoryContainer.CreateItemAsync(newDoc, partitionKey);
            }
        }

        }
}
