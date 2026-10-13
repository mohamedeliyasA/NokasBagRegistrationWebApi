using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Nokas.CashMgmt.BagRegistration.Func.Configuration;

namespace Nokas.CashMgmt.BagRegistration.Func.Services;

public sealed class CountingResultResyncService(
    CosmosClient cosmosClient,
    CosmosDbOptions cosmosOptions,
    ICountingResultDataService dataService,
    ICountingResultSyncService syncService,
    ILogger<CountingResultResyncService> logger) : ICountingResultResyncService
{
    private static readonly TimeSpan DefaultBackfillWindow = TimeSpan.FromDays(548);

    public async Task<CountingResultResyncResult> ResyncCustomerAsync(
        string customerNumber, DateTime? fromDate, bool purgeOtherClients, CancellationToken cancellationToken)
    {
        var mappings = await dataService.GetCustomerMappingsAsync(cancellationToken);
        var mapping = mappings.FirstOrDefault(m => m.NokasCustomerNumber == customerNumber);

        if (mapping is null)
        {
            logger.LogWarning("Resync requested for unmapped customer number {CustomerNumber} - nothing to do.", customerNumber);
            return new CountingResultResyncResult(CustomerFound: false, RowsUpserted: 0, StaleDocsPurged: 0);
        }

        var staleDocsPurged = 0;
        if (purgeOtherClients)
        {
            staleDocsPurged = await PurgeStaleClientPartitionsAsync(customerNumber, mapping.ClientID, cancellationToken);
        }

        var effectiveFromDate = fromDate ?? mapping.BackfillFromDate ?? DateTime.UtcNow.Subtract(DefaultBackfillWindow);
        var rowsUpserted = await syncService.BackfillCustomerAsync(mapping, effectiveFromDate, cancellationToken);

        logger.LogInformation(
            "Resync complete for customer {CustomerNumber} (clientId {ClientId}): {RowsUpserted} rows upserted, {StaleDocsPurged} stale docs purged, fromDate {FromDate}.",
            customerNumber, mapping.ClientID, rowsUpserted, staleDocsPurged, effectiveFromDate);

        return new CountingResultResyncResult(CustomerFound: true, RowsUpserted: rowsUpserted, StaleDocsPurged: staleDocsPurged);
    }

    /// <summary>Deletes any countingresults docs for this customer number sitting under a
    /// clientId other than its current mapping - repairs a prior clientId/partition correction.
    /// Cross-partition query - fine for this low-volume, internal-only, function-key-gated tool.</summary>
    private async Task<int> PurgeStaleClientPartitionsAsync(string customerNumber, string correctClientId, CancellationToken cancellationToken)
    {
        var container = cosmosClient.GetContainer(cosmosOptions.DatabaseName, cosmosOptions.CountingResultContainerName);

        var query = new QueryDefinition("SELECT c.id, c.clientId FROM c WHERE c.customerNumber = @customerNumber AND c.clientId != @correctClientId")
            .WithParameter("@customerNumber", customerNumber)
            .WithParameter("@correctClientId", correctClientId);

        var purged = 0;
        using var iterator = container.GetItemQueryIterator<StaleDocRef>(query);
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            foreach (var doc in page)
            {
                await container.DeleteItemAsync<object>(doc.Id, new PartitionKey(doc.ClientId), cancellationToken: cancellationToken);
                purged++;
            }
        }

        return purged;
    }

    private sealed record StaleDocRef(string Id, string ClientId);
}
