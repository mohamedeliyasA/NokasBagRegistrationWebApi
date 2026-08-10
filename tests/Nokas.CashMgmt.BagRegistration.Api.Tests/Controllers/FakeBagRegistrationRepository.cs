using Nokas.CashMgmt.BagRegistration.Api.Documents;
using Nokas.CashMgmt.BagRegistration.Api.Repositories;
using Nokas.CashMgmt.Shared.Results;

namespace Nokas.CashMgmt.BagRegistration.Api.Tests.Controllers;

public sealed class FakeBagRegistrationRepository : IBagRegistrationRepository
{
    public Dictionary<string, BagRegistrationDocument> Documents { get; } = new();
    public List<OutboxMessageDocument> WrittenOutboxMessages { get; } = new();
    public Result<BagRegistrationDocument>? UpdateWithOutboxResultOverride { get; set; }

    public Task<Result<BagRegistrationDocument>> CreateAsync(
        BagRegistrationDocument document, OutboxMessageDocument outboxMessage, CancellationToken cancellationToken)
    {
        Documents[document.Id] = document;
        WrittenOutboxMessages.Add(outboxMessage);
        return Task.FromResult(Result<BagRegistrationDocument>.Success(document));
    }

    public Task<Result<BagRegistrationDocument>> GetByBagNumberForClientAsync(
        string bagNumber, string clientId, CancellationToken cancellationToken)
    {
        if (Documents.TryGetValue(bagNumber, out var document) && document.ClientId == clientId)
        {
            return Task.FromResult(Result<BagRegistrationDocument>.Success(document));
        }

        return Task.FromResult<Result<BagRegistrationDocument>>(
            Error.NotFound("ERR_BAG_NOT_FOUND", $"No bag found with bag number '{bagNumber}' for this client."));
    }

    public Task<Result<BagRegistrationDocument>> UpdateAsync(BagRegistrationDocument document, CancellationToken cancellationToken)
    {
        Documents[document.Id] = document;
        return Task.FromResult(Result<BagRegistrationDocument>.Success(document));
    }

    public Task<Result<BagRegistrationDocument>> UpdateWithOutboxAsync(
        BagRegistrationDocument document, OutboxMessageDocument outboxMessage, CancellationToken cancellationToken)
    {
        if (UpdateWithOutboxResultOverride is { } overrideResult)
        {
            return Task.FromResult(overrideResult);
        }

        Documents[document.Id] = document;
        WrittenOutboxMessages.Add(outboxMessage);
        return Task.FromResult(Result<BagRegistrationDocument>.Success(document));
    }
}
