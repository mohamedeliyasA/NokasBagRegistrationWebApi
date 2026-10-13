using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using Nokas.CashMgmt.No.Api.Configuration;
using Nokas.CashMgmt.No.Shared.Documents;
using Nokas.CashMgmt.No.Shared.StatusMapping;

namespace Nokas.CashMgmt.No.Api.Repositories;

public sealed class CosmosCountingResultRepository : ICountingResultRepository
{
    private readonly Container _container;

    public CosmosCountingResultRepository(CosmosClient cosmosClient, IOptions<CosmosDbOptions> options)
    {
        var config = options.Value;
        _container = cosmosClient.GetContainer(config.DatabaseName, config.CountingResultContainerName);
    }

    public async Task<CountingResultPage> QueryAsync(
        string clientId, CountingResultQueryOptions query, string? nativeContinuationToken, CancellationToken cancellationToken)
    {
        var dateField = query.DateType switch
        {
            "registered" => "registeredAt",
            "lastUpdated" => "lastUpdatedAt",
            _ => "countedAt"
        };

        var sql = $"SELECT * FROM c WHERE c.clientId = @clientId AND c.{dateField} >= @fromDate AND c.{dateField} <= @toDate";

        if (query.DateType == "counted")
        {
            sql += " AND IS_DEFINED(c.countedAt) AND c.countedAt != null";
        }

        if (!string.IsNullOrEmpty(query.CustomerRefNumber))
        {
            sql += " AND c.customerRefNumber = @customerRefNumber";
        }

        if (!string.IsNullOrEmpty(query.StatusBucket))
        {
            var labels = NattsafeStatusMapper.GranularLabelsForBucket(query.StatusBucket);
            var paramNames = string.Join(", ", Enumerable.Range(0, labels.Count).Select(i => $"@status{i}"));
            sql += $" AND c.status IN ({paramNames})";
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

        if (!string.IsNullOrEmpty(query.StatusBucket))
        {
            var labels = NattsafeStatusMapper.GranularLabelsForBucket(query.StatusBucket);
            for (var i = 0; i < labels.Count; i++)
            {
                queryDefinition = queryDefinition.WithParameter($"@status{i}", labels[i]);
            }
        }

        using var iterator = _container.GetItemQueryIterator<CountingResultDocument>(
            queryDefinition,
            nativeContinuationToken,
            new QueryRequestOptions
            {
                MaxItemCount = query.PageSize,
                PartitionKey = new PartitionKey(clientId)
            });

        var response = await iterator.ReadNextAsync(cancellationToken);
        return new CountingResultPage(response.ToList(), response.ContinuationToken);
    }

    public async Task<CountingResultDocument?> GetLatestByBagNumberAsync(string clientId, string bagNumber, CancellationToken cancellationToken)
    {
        var queryDefinition = new QueryDefinition(
            "SELECT * FROM c WHERE c.clientId = @clientId AND c.bagNumber = @bagNumber ORDER BY c.lastUpdatedAt DESC OFFSET 0 LIMIT 1")
            .WithParameter("@clientId", clientId)
            .WithParameter("@bagNumber", bagNumber);

        using var iterator = _container.GetItemQueryIterator<CountingResultDocument>(
            queryDefinition,
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(clientId), MaxItemCount = 1 });

        if (!iterator.HasMoreResults)
        {
            return null;
        }

        var response = await iterator.ReadNextAsync(cancellationToken);
        return response.FirstOrDefault();
    }

    public async Task<DateTime?> GetDataAsOfAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await _container.ReadItemAsync<CountingResultSyncStateDocument>(
                CountingResultSyncStateDocument.DocumentId,
                new PartitionKey(CountingResultSyncStateDocument.SentinelClientId),
                cancellationToken: cancellationToken);
            return response.Resource.LastSuccessfulRunUtc;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }
}
