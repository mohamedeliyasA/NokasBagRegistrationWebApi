using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using Nokas.CashMgmt.Dk.BagRegistration.Api.Configuration;
using Nokas.CashMgmt.Dk.BagRegistration.Api.Documents;
using Nokas.CashMgmt.Dk.Shared.Documents;

namespace Nokas.CashMgmt.Dk.BagRegistration.Api.Repositories;

public sealed class CosmosDkBagRegistrationRepository : IDkBagRegistrationRepository
{
    private readonly Container _container;

    public CosmosDkBagRegistrationRepository(CosmosClient client, IOptions<CosmosDbOptions> options)
    {
        var value = options.Value;
        _container = client.GetContainer(value.DatabaseName, value.RegistrationContainerName);
    }

    public Task SaveAsync(DkBagRegistrationDocument document, CancellationToken cancellationToken) =>
        _container.CreateItemAsync(document, new PartitionKey(document.ClientId), cancellationToken: cancellationToken);

    public async Task<DkBagRegistrationDocument?> GetLatestByBagNumberAsync(
        string bagNumber, string clientId, CancellationToken cancellationToken)
    {
        // Single-partition query with a single ORDER BY property, so the default indexing
        // policy covers it without a composite index. Rejected submissions never populate
        // bagNumber, so they cannot be returned here.
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.bagNumber = @bagNumber AND c.accepted = true ORDER BY c.receivedAtUtc DESC OFFSET 0 LIMIT 1")
            .WithParameter("@bagNumber", bagNumber);

        using var iterator = _container.GetItemQueryIterator<DkBagRegistrationDocument>(
            query,
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(clientId),
                MaxItemCount = 1
            });

        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            foreach (var document in page)
            {
                return document;
            }
        }

        return null;
    }
}
