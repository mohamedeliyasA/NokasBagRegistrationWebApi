using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Nokas.CashMgmt.Dk.Shared.Configuration;
using Nokas.CashMgmt.Dk.Shared.Documents;

using Microsoft.Extensions.Logging;

namespace Nokas.CashMgmt.Dk.Shared.Repositories;

/// <summary>
/// First-write-wins tenancy claim, made atomic by Cosmos itself: <c>CreateItemAsync</c> with
/// id = kardex succeeds only if no document with that id exists yet, so two concurrent
/// registrations for a brand-new kardex race safely - exactly one of them gets ClaimedNew, the
/// other gets the 409 path below and re-reads to discover it was the same clientId
/// (AlreadyOwnedBySameClient) or a different one (OwnedByDifferentClient).
/// </summary>
public sealed class CosmosDkKardexMapRepository : IDkKardexMapRepository
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    private readonly Container _container;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CosmosDkKardexMapRepository> _logger;

    public CosmosDkKardexMapRepository(
        CosmosClient client, IOptions<DkCosmosOptions> options, IMemoryCache cache, ILogger<CosmosDkKardexMapRepository> logger)
    {
        var value = options.Value;
        _container = client.GetContainer(value.DatabaseName, value.KardexMapContainerName);
        _cache = cache;
        _logger = logger;
    }

    public async Task<KardexClaimOutcome> ClaimOrVerifyAsync(
        string customerRefNumber, string clientId, string claimedBy, CancellationToken cancellationToken)
    {
        var cacheKey = CacheKey(customerRefNumber);
        if (_cache.TryGetValue(cacheKey, out string? cachedOwner))
        {
            return cachedOwner == clientId ? KardexClaimOutcome.AlreadyOwnedBySameClient : KardexClaimOutcome.OwnedByDifferentClient;
        }

        var document = new DkKardexMapDocument
        {
            Id = customerRefNumber,
            CustomerRefNumber = customerRefNumber,
            ClientId = clientId,
            ClaimedBy = claimedBy
        };

        try
        {
            await _container.CreateItemAsync(document, new PartitionKey(customerRefNumber), cancellationToken: cancellationToken);
            _cache.Set(cacheKey, clientId, CacheDuration);
            _logger.LogInformation(
                "Kardex {CustomerRefNumber} claimed for the first time by clientId {ClientId} (via {ClaimedBy}).",
                customerRefNumber, clientId, claimedBy);
            return KardexClaimOutcome.ClaimedNew;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            var owner = await TryGetOwnerAsync(customerRefNumber, cancellationToken);

            if (owner is null)
            {
                // Extremely unlikely (deleted between the 409 and this read) - treat as unclaimed
                // rather than guessing; the next attempt will claim it cleanly.
                return KardexClaimOutcome.OwnedByDifferentClient;
            }

            _cache.Set(cacheKey, owner, CacheDuration);

            if (owner == clientId)
            {
                return KardexClaimOutcome.AlreadyOwnedBySameClient;
            }

            _logger.LogWarning(
                "Kardex {CustomerRefNumber} is claimed by clientId {Owner} but clientId {AttemptedClientId} just tried to use it too - treating as a tenancy conflict, not reassigning.",
                customerRefNumber, owner, clientId);
            return KardexClaimOutcome.OwnedByDifferentClient;
        }
    }

    public async Task<string?> TryGetOwnerAsync(string customerRefNumber, CancellationToken cancellationToken)
    {
        var cacheKey = CacheKey(customerRefNumber);
        if (_cache.TryGetValue(cacheKey, out string? cached))
        {
            return cached;
        }

        try
        {
            var response = await _container.ReadItemAsync<DkKardexMapDocument>(
                customerRefNumber, new PartitionKey(customerRefNumber), cancellationToken: cancellationToken);
            _cache.Set(cacheKey, response.Resource.ClientId, CacheDuration);
            return response.Resource.ClientId;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private static string CacheKey(string customerRefNumber) => $"dk-kardex-owner:{customerRefNumber}";
}
