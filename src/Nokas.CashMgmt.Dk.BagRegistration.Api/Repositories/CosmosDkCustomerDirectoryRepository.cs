using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Nokas.CashMgmt.Dk.BagRegistration.Api.Configuration;
using Nokas.CashMgmt.Dk.BagRegistration.Api.Documents;
using Nokas.CashMgmt.Dk.Shared.Documents;

namespace Nokas.CashMgmt.Dk.BagRegistration.Api.Repositories;

/// <summary>
/// Point read against the directory container, wrapped in a short-lived in-memory cache -
/// every registration looks this up, and the directory changes only when a partner is
/// onboarded, so a per-request Cosmos round trip is unnecessary cost.
/// </summary>
public sealed class CosmosDkCustomerDirectoryRepository : IDkCustomerDirectoryRepository
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    private readonly Container _container;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CosmosDkCustomerDirectoryRepository> _logger;

    public CosmosDkCustomerDirectoryRepository(
        CosmosClient client,
        IOptions<CosmosDbOptions> options,
        IMemoryCache cache,
        ILogger<CosmosDkCustomerDirectoryRepository> logger)
    {
        var value = options.Value;
        _container = client.GetContainer(value.DatabaseName, value.CustomerDirectoryContainerName);
        _cache = cache;
        _logger = logger;
    }

    public async Task<string?> GetCustomerNameAsync(string clientId, CancellationToken cancellationToken)
    {
        var cacheKey = $"dk-customer-name:{clientId}";

        if (_cache.TryGetValue(cacheKey, out string? cached))
        {
            return cached;
        }

        string? name;
        try
        {
            var response = await _container.ReadItemAsync<DkCustomerDirectoryDocument>(
                clientId, new PartitionKey(clientId), cancellationToken: cancellationToken);
            name = response.Resource.Active ? response.Resource.CustomerName : null;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            name = null;
        }
        catch (Exception ex)
        {
            // The directory is an enrichment, not a dependency - a Cosmos hiccup here must
            // never block a registration or a ServiceNow forward. Cache nothing so the next
            // call retries rather than pinning a transient failure for 5 minutes.
            _logger.LogWarning(ex, "Could not read the DK customer directory for clientId {ClientId}.", clientId);
            return null;
        }

        _cache.Set(cacheKey, name, CacheDuration);
        return name;
    }
}
