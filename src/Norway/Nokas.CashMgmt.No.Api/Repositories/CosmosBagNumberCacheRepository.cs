using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using Nokas.CashMgmt.No.Api.Configuration;
using Nokas.CashMgmt.No.Shared.Documents;

namespace Nokas.CashMgmt.No.Api.Repositories;

public sealed class CosmosBagNumberCacheRepository : IBagNumberCacheRepository
{
    private readonly Container _container;

    public CosmosBagNumberCacheRepository(CosmosClient cosmosClient, IOptions<CosmosDbOptions> options)
    {
        var config = options.Value;
        _container = cosmosClient.GetContainer(config.DatabaseName, config.BagNumberCacheContainerName);
    }

    public async Task<bool> ExistsAsync(string bagNumber, CancellationToken cancellationToken)
    {
        try
        {
            await _container.ReadItemAsync<BagNumberCacheDocument>(
                bagNumber, new PartitionKey(bagNumber), cancellationToken: cancellationToken);
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task<DateTime?> GetLastSyncedAtAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await _container.ReadItemAsync<SyncStateDocument>(
                SyncStateDocument.DocumentId, new PartitionKey(SyncStateDocument.DocumentId), cancellationToken: cancellationToken);
            return response.Resource.LastSuccessfulRunUtc;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }
}
