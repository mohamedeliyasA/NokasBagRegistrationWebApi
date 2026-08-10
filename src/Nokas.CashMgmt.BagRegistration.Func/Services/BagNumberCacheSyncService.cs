using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Nokas.CashMgmt.BagRegistration.Func.Configuration;
using Nokas.CashMgmt.Shared.Documents;

namespace Nokas.CashMgmt.BagRegistration.Func.Services;

public sealed class BagNumberCacheSyncService(
    CosmosClient cosmosClient,
    CosmosDbOptions cosmosOptions,
    IBagStatusService bagStatusService,
    ILogger<BagNumberCacheSyncService> logger) : IBagNumberCacheSyncService
{
    private static readonly TimeSpan Overlap = TimeSpan.FromMinutes(2);
    private const int PageSize = 2000;
    private const int MaxPagesPerRun = 10;
    private static readonly DateTime DefaultBackfillStart = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var container = cosmosClient.GetContainer(cosmosOptions.DatabaseName, cosmosOptions.BagNumberCacheContainerName);

        var watermark = await GetWatermarkAsync(container, cancellationToken);
        var queryFrom = watermark - Overlap;

        var totalUpserted = 0;
        for (var page = 0; page < MaxPagesPerRun; page++)
        {
            var rows = await bagStatusService.GetBagNumbersCreatedAfterAsync(queryFrom, PageSize, cancellationToken);
            if (rows.Count == 0)
            {
                break;
            }

            foreach (var row in rows)
            {
                var doc = new BagNumberCacheDocument { Id = row.BagNumber, SyncedAt = DateTime.UtcNow };
                await container.UpsertItemAsync(doc, new PartitionKey(doc.Id), cancellationToken: cancellationToken);
            }

            totalUpserted += rows.Count;
            queryFrom = rows[^1].CreatedAt;

            // Commit the watermark after every page, not just at the end of the run - a page
            // that's already upserted is safe to record as done, so a run that stops partway
            // (crash, timeout, hitting MaxPagesPerRun) still leaves real, resumable progress.
            await SaveWatermarkAsync(container, queryFrom, cancellationToken);

            if (rows.Count < PageSize)
            {
                break;
            }
        }

        logger.LogInformation("Bag-number cache sync run complete. Rows upserted: {Count}, watermark: {Watermark}",
            totalUpserted, queryFrom);
    }

    private static async Task<DateTime> GetWatermarkAsync(Container container, CancellationToken cancellationToken)
    {
        try
        {
            var response = await container.ReadItemAsync<SyncStateDocument>(
                SyncStateDocument.DocumentId, new PartitionKey(SyncStateDocument.DocumentId), cancellationToken: cancellationToken);
            return response.Resource.LastSuccessfulRunUtc;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return DefaultBackfillStart;
        }
    }

    private static async Task SaveWatermarkAsync(Container container, DateTime watermark, CancellationToken cancellationToken)
    {
        var doc = new SyncStateDocument { LastSuccessfulRunUtc = watermark };
        await container.UpsertItemAsync(doc, new PartitionKey(SyncStateDocument.DocumentId), cancellationToken: cancellationToken);
    }
}
