using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using Nokas.CashMgmt.No.Api.Configuration;
using Nokas.CashMgmt.No.Api.Documents;
using Nokas.CashMgmt.Common.Results;

namespace Nokas.CashMgmt.No.Api.Repositories;

public sealed class CosmosBagRegistrationRepository : IBagRegistrationRepository
{
    private readonly Container _container;
    private readonly ILogger<CosmosBagRegistrationRepository> _logger;

    public CosmosBagRegistrationRepository(
        CosmosClient cosmosClient,
        IOptions<CosmosDbOptions> options,
        ILogger<CosmosBagRegistrationRepository> logger)
    {
        var config = options.Value;
        _container = cosmosClient.GetContainer(config.DatabaseName, config.BagRegistrationContainerName);
        _logger = logger;
    }

    public async Task<Result<BagRegistrationDocument>> CreateAsync(
        BagRegistrationDocument document,
        OutboxMessageDocument outboxMessage,
        CancellationToken cancellationToken)
    {
        outboxMessage.ClientId = document.ClientId;

        var batch = _container.CreateTransactionalBatch(new PartitionKey(document.ClientId))
            .CreateItem(document)
            .CreateItem(outboxMessage);

        using var response = await batch.ExecuteAsync(cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return Result<BagRegistrationDocument>.Success(document);
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            // Logged by the caller, where the outcome (conflict vs. real failure) is known.
            return Error.Conflict("ERR_DUPLICATE_BAG", $"Bag number '{document.BagRegistration.BagNumber}' already exists.", "BagNumber");
        }

        // A batch reports one status for the whole transaction; the failing operation
        // carries the real status, others report 424 FailedDependency. Log each
        // operation's own result to identify which one actually failed.
        var perOperation = new List<string>();
        for (var i = 0; i < response.Count; i++)
        {
            var opName = i == 0 ? "bagDocument" : "outboxMessage";
            perOperation.Add($"[{i}] {opName} => {(int)response[i].StatusCode} {response[i].StatusCode}");
        }

        _logger.LogCritical(
            "Transactional batch failed with status {StatusCode} for BagNumber: {BagNumber}. ErrorMessage: {ErrorMessage}. Operations: {Operations}. ActivityId: {ActivityId}",
            response.StatusCode, document.BagRegistration.BagNumber, response.ErrorMessage ?? "(none)",
            string.Join(" | ", perOperation), response.ActivityId);

        return Error.Internal("ERR_COSMOS_GENERIC", "A database error occurred while processing your request.");
    }

    public async Task<Result<BagRegistrationDocument>> GetByBagNumberForClientAsync(
        string bagNumber,
        string clientId,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _container.ReadItemAsync<BagRegistrationDocument>(
                bagNumber, new PartitionKey(clientId), cancellationToken: cancellationToken);
            return Result<BagRegistrationDocument>.Success(response.Resource);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return Error.NotFound("ERR_BAG_NOT_FOUND", $"No bag found with bag number '{bagNumber}' for this client.");
        }
    }

    public async Task<Result<BagRegistrationDocument>> UpdateAsync(
        BagRegistrationDocument document,
        CancellationToken cancellationToken)
    {
        await _container.UpsertItemAsync(document, new PartitionKey(document.ClientId), cancellationToken: cancellationToken);
        return Result<BagRegistrationDocument>.Success(document);
    }

    public async Task<Result<BagRegistrationDocument>> UpdateWithOutboxAsync(
        BagRegistrationDocument document,
        OutboxMessageDocument outboxMessage,
        CancellationToken cancellationToken)
    {
        outboxMessage.ClientId = document.ClientId;

        var batch = _container.CreateTransactionalBatch(new PartitionKey(document.ClientId))
            .UpsertItem(document)
            .CreateItem(outboxMessage);

        using var response = await batch.ExecuteAsync(cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return Result<BagRegistrationDocument>.Success(document);
        }

        var perOperation = new List<string>();
        for (var i = 0; i < response.Count; i++)
        {
            var opName = i == 0 ? "bagDocument" : "outboxMessage";
            perOperation.Add($"[{i}] {opName} => {(int)response[i].StatusCode} {response[i].StatusCode}");
        }

        _logger.LogCritical(
            "Transactional batch failed with status {StatusCode} for BagNumber: {BagNumber}. ErrorMessage: {ErrorMessage}. Operations: {Operations}. ActivityId: {ActivityId}",
            response.StatusCode, document.BagRegistration.BagNumber, response.ErrorMessage ?? "(none)",
            string.Join(" | ", perOperation), response.ActivityId);

        return Error.Internal("ERR_COSMOS_GENERIC", "A database error occurred while processing your request.");
    }
}