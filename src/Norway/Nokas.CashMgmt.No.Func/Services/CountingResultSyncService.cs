using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Nokas.CashMgmt.No.Func.Configuration;
using Nokas.CashMgmt.No.Func.Models;
using Nokas.CashMgmt.No.Shared.Documents;
using Nokas.CashMgmt.No.Shared.StatusMapping;

namespace Nokas.CashMgmt.No.Func.Services;

/// <summary>
/// Syncs Nattsafe into Cosmos on a timer. Two independent watermarks: statusTrackingWatermark
/// drives the regular delta pass over already-onboarded customers; mappingWatermark drives
/// one-time backfill of newly onboarded customers. A run that throws never reaches
/// SaveSyncStateAsync, so a failed run never advances either watermark.
/// </summary>
public sealed class CountingResultSyncService(
    CosmosClient cosmosClient,
    CosmosDbOptions cosmosOptions,
    ICountingResultDataService dataService,
    ILogger<CountingResultSyncService> logger) : ICountingResultSyncService
{
    private static readonly TimeSpan Overlap = TimeSpan.FromMinutes(10);
    private const int PageSize = 500;
    private const int MaxPagesPerRun = 20;
    private static readonly TimeSpan DefaultBackfillWindow = TimeSpan.FromDays(548);
    private static readonly DateTime DefaultSyncStart = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var container = cosmosClient.GetContainer(cosmosOptions.DatabaseName, cosmosOptions.CountingResultContainerName);

        var state = await GetSyncStateAsync(container, cancellationToken);
        var mappings = await dataService.GetCustomerMappingsAsync(cancellationToken);

        // First mapping wins if a customer number appears more than once - phase-1 scope.
        var mappingByCustomerNumber = new Dictionary<string, CustomerMappingRow>();
        foreach (var m in mappings)
        {
            mappingByCustomerNumber.TryAdd(m.NokasCustomerNumber, m);
        }

        var newStatusTrackingWatermark = state.StatusTrackingWatermark;
        var newMappingWatermark = state.MappingWatermark;
        var totalUpserted = 0;

        var newCustomers = mappings.Where(m => m.CreatedAt > state.MappingWatermark).ToList();
        foreach (var customer in newCustomers)
        {
            var backfillFrom = customer.BackfillFromDate ?? DateTime.UtcNow.Subtract(DefaultBackfillWindow);
            totalUpserted += await BackfillCustomerAsync(customer, backfillFrom, cancellationToken);

            if (customer.CreatedAt > newMappingWatermark)
            {
                newMappingWatermark = customer.CreatedAt;
            }
        }

        var allCustomerNumbers = mappingByCustomerNumber.Keys.ToList();
        if (allCustomerNumbers.Count > 0)
        {
            var deltaFrom = state.StatusTrackingWatermark - Overlap;
            var (count, lastSeen) = await RunPagesAsync(container, "Delta", deltaFrom, allCustomerNumbers, mappingByCustomerNumber, cancellationToken);
            totalUpserted += count;
            if (lastSeen > newStatusTrackingWatermark)
            {
                newStatusTrackingWatermark = lastSeen;
            }
        }

        await SaveSyncStateAsync(container, newStatusTrackingWatermark, newMappingWatermark, cancellationToken);

        logger.LogInformation(
            "Counting-result sync run complete. Rows upserted: {Count}, statusTrackingWatermark: {StatusWatermark}, mappingWatermark: {MappingWatermark}, backfilledCustomers: {BackfilledCount}",
            totalUpserted, newStatusTrackingWatermark, newMappingWatermark, newCustomers.Count);
    }

    public async Task<int> BackfillCustomerAsync(CustomerMappingRow mapping, DateTime fromDate, CancellationToken cancellationToken)
    {
        var container = cosmosClient.GetContainer(cosmosOptions.DatabaseName, cosmosOptions.CountingResultContainerName);
        var mappingByCustomerNumber = new Dictionary<string, CustomerMappingRow> { [mapping.NokasCustomerNumber] = mapping };
        var (count, _) = await RunPagesAsync(container, "Backfill", fromDate, [mapping.NokasCustomerNumber], mappingByCustomerNumber, cancellationToken);
        return count;
    }

    private async Task<(int Count, DateTime LastSeen)> RunPagesAsync(
        Container container, string mode, DateTime sinceDate, IReadOnlyCollection<string> customerNumbers,
        IReadOnlyDictionary<string, CustomerMappingRow> mappingByCustomerNumber, CancellationToken cancellationToken)
    {
        var cursor = sinceDate;
        var total = 0;
        var lastSeen = sinceDate;

        for (var page = 0; page < MaxPagesPerRun; page++)
        {
            var (changes, history) = await dataService.FetchChangesAsync(mode, cursor, customerNumbers, PageSize, cancellationToken);
            if (changes.Count == 0)
            {
                break;
            }

            var historyByRecord = history.GroupBy(h => h.NattsafeRecordId).ToDictionary(g => g.Key, g => (IReadOnlyList<CountingResultHistoryRow>)g.ToList());

            foreach (var row in changes)
            {
                if (row.CustomerNumber is null || !mappingByCustomerNumber.TryGetValue(row.CustomerNumber, out var mapping))
                {
                    // Not (or no longer) a mapped/onboarded customer - only mapped customers are ever synced.
                    continue;
                }

                var doc = ToDocument(row, mapping, historyByRecord.GetValueOrDefault(row.NattsafeRecordId, []));
                await container.UpsertItemAsync(doc, new PartitionKey(doc.ClientId), cancellationToken: cancellationToken);
            }

            total += changes.Count;
            lastSeen = changes[^1].LastUpdatedAt;
            cursor = lastSeen;

            if (changes.Count < PageSize)
            {
                break;
            }
        }

        return (total, lastSeen);
    }

    private static CountingResultDocument ToDocument(CountingResultChangeRow row, CustomerMappingRow mapping, IReadOnlyList<CountingResultHistoryRow> history)
    {
        var registrationChannel = row.MyReference is not null && row.MyReference.StartsWith("API_", StringComparison.OrdinalIgnoreCase)
            ? "API"
            : "Portal";

        return new CountingResultDocument
        {
            Id = row.NattsafeRecordId.ToString(),
            ClientId = mapping.ClientID,
            NattsafeRecordId = row.NattsafeRecordId,
            BagNumber = row.BagNumber,
            CustomerRefNumber = mapping.CustomerRefNumber,
            CustomerNumber = mapping.NokasCustomerNumber,
            Status = NattsafeStatusMapper.ToLabel(row.StatusCode, row.TransportFetchedAt),
            StatusCode = row.StatusCode ?? -1,
            DeclaredAmount = (decimal?)row.DeclaredAmount,
            CountedAmount = (decimal?)row.CountedAmount,
            DeviationAmount = row.CountedAmount.HasValue && row.DeclaredAmount.HasValue ? (decimal?)(row.CountedAmount - row.DeclaredAmount) : null,
            RegisteredAt = row.RegisteredAt,
            CountedAt = row.CountedAt,
            TransportFetchedAt = row.TransportFetchedAt,
            LastUpdatedAt = row.LastUpdatedAt,
            RegistrationChannel = registrationChannel,
            StatusHistory = [.. history.OrderBy(h => h.ChangedAt).Select(h => new CountingResultStatusHistoryEntry
            {
                StatusCode = h.StatusCode,
                Status = NattsafeStatusMapper.ToLabel(h.StatusCode),
                ChangedAt = h.ChangedAt
            })]
        };
    }

    private static async Task<CountingResultSyncStateDocument> GetSyncStateAsync(Container container, CancellationToken cancellationToken)
    {
        try
        {
            var response = await container.ReadItemAsync<CountingResultSyncStateDocument>(
                CountingResultSyncStateDocument.DocumentId,
                new PartitionKey(CountingResultSyncStateDocument.SentinelClientId),
                cancellationToken: cancellationToken);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return new CountingResultSyncStateDocument
            {
                StatusTrackingWatermark = DefaultSyncStart,
                MappingWatermark = DefaultSyncStart,
                LastSuccessfulRunUtc = DefaultSyncStart
            };
        }
    }

    private static async Task SaveSyncStateAsync(Container container, DateTime statusTrackingWatermark, DateTime mappingWatermark, CancellationToken cancellationToken)
    {
        var doc = new CountingResultSyncStateDocument
        {
            StatusTrackingWatermark = statusTrackingWatermark,
            MappingWatermark = mappingWatermark,
            LastSuccessfulRunUtc = DateTime.UtcNow
        };
        await container.UpsertItemAsync(doc, new PartitionKey(CountingResultSyncStateDocument.SentinelClientId), cancellationToken: cancellationToken);
    }
}
