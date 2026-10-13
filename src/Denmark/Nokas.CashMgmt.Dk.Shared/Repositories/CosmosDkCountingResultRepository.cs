using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using Nokas.CashMgmt.Dk.Shared.Configuration;
using Nokas.CashMgmt.Dk.Shared.Documents;

namespace Nokas.CashMgmt.Dk.Shared.Repositories;

/// <summary>
/// Query shape mirrors Norway's CosmosCountingResultRepository: filter and order on the chosen
/// date (registered | counted | lastUpdated), with the status vocabulary in
/// DkCountingResultStatusMapper. A row with no value for the chosen date (e.g. not yet counted)
/// never matches a range on it.
/// </summary>
public sealed class CosmosDkCountingResultRepository : IDkCountingResultRepository
{
    private readonly Container _container;

    public CosmosDkCountingResultRepository(CosmosClient cosmosClient, IOptions<DkCosmosOptions> options)
    {
        var config = options.Value;
        _container = cosmosClient.GetContainer(config.DatabaseName, config.CountingResultContainerName);
    }

    public async Task<DkCountingResultPage> QueryAsync(
        string clientId, DkCountingResultQueryOptions query, string? nativeContinuationToken, CancellationToken cancellationToken)
    {
        var dateField = query.DateType switch
        {
            "registered" => "registeredAt",
            "counted" => "countedAt",
            _ => "lastUpdatedAt"
        };

        var sql = $"SELECT * FROM c WHERE c.clientId = @clientId AND c.{dateField} >= @fromDate AND c.{dateField} <= @toDate";

        if (!string.IsNullOrEmpty(query.CustomerRefNumber))
        {
            sql += " AND c.customerRefNumber = @customerRefNumber";
        }

        if (!string.IsNullOrEmpty(query.Status))
        {
            sql += " AND c.status = @status";
        }

        sql += $" ORDER BY c.{dateField} DESC";

        var queryDefinition = new QueryDefinition(sql)
            .WithParameter("@clientId", clientId)
            .WithParameter("@fromDate", query.FromDate)
            .WithParameter("@toDate", query.ToDate);

        if (!string.IsNullOrEmpty(query.CustomerRefNumber))
        {
            queryDefinition = queryDefinition.WithParameter("@customerRefNumber", query.CustomerRefNumber);
        }

        if (!string.IsNullOrEmpty(query.Status))
        {
            queryDefinition = queryDefinition.WithParameter("@status", query.Status);
        }

        using var iterator = _container.GetItemQueryIterator<DkCountingResultDocument>(
            queryDefinition,
            nativeContinuationToken,
            new QueryRequestOptions
            {
                MaxItemCount = query.PageSize,
                PartitionKey = new PartitionKey(clientId)
            });

        var response = await iterator.ReadNextAsync(cancellationToken);
        return new DkCountingResultPage(response.ToList(), response.ContinuationToken);
    }

    public async Task<DkCountingResultDocument?> GetLatestByBagNumberAsync(string clientId, string bagNumber, CancellationToken cancellationToken)
    {
        var queryDefinition = new QueryDefinition(
            "SELECT * FROM c WHERE c.clientId = @clientId AND c.bagNumber = @bagNumber ORDER BY c.lastUpdatedAt DESC OFFSET 0 LIMIT 1")
            .WithParameter("@clientId", clientId)
            .WithParameter("@bagNumber", bagNumber);

        using var iterator = _container.GetItemQueryIterator<DkCountingResultDocument>(
            queryDefinition,
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(clientId), MaxItemCount = 1 });

        if (!iterator.HasMoreResults)
        {
            return null;
        }

        var response = await iterator.ReadNextAsync(cancellationToken);
        return response.FirstOrDefault();
    }

    public async Task<DateTime?> GetDataAsOfAsync(CancellationToken cancellationToken) =>
        (await ReadSyncStateAsync(cancellationToken))?.LastSuccessfulRunUtc;

    private async Task<DkCountingResultSyncStateDocument?> ReadSyncStateAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await _container.ReadItemAsync<DkCountingResultSyncStateDocument>(
                DkCountingResultSyncStateDocument.SentinelId,
                new PartitionKey(DkCountingResultSyncStateDocument.SentinelId),
                cancellationToken: cancellationToken);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<DateTime?> GetWatermarkAsync(CancellationToken cancellationToken) =>
        (await ReadSyncStateAsync(cancellationToken))?.LastModifiedWatermarkUtc;

    public Task UpsertAsync(DkCountingResultDocument document, CancellationToken cancellationToken) =>
        _container.UpsertItemAsync(document, new PartitionKey(document.ClientId), cancellationToken: cancellationToken);

    public Task RecordSyncRunAsync(
        DateTime completedAtUtc, DateTime watermarkUtc, int rowsFetched, int rowsUpserted, int rowsSkippedUnclaimed, CancellationToken cancellationToken)
    {
        var document = new DkCountingResultSyncStateDocument
        {
            LastSuccessfulRunUtc = completedAtUtc,
            LastModifiedWatermarkUtc = watermarkUtc,
            LastRunRowsFetched = rowsFetched,
            LastRunRowsUpserted = rowsUpserted,
            LastRunRowsSkippedUnclaimed = rowsSkippedUnclaimed
        };

        return _container.UpsertItemAsync(
            document, new PartitionKey(DkCountingResultSyncStateDocument.SentinelId), cancellationToken: cancellationToken);
    }
}
