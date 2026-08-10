using Nokas.CashMgmt.BagRegistration.Api.Documents;
using Nokas.CashMgmt.Shared.Results;

namespace Nokas.CashMgmt.BagRegistration.Api.Repositories;

public interface IBagRegistrationRepository
{
    Task<Result<BagRegistrationDocument>> CreateAsync(
        BagRegistrationDocument document,
        OutboxMessageDocument outboxMessage,
        CancellationToken cancellationToken);

    Task<Result<BagRegistrationDocument>> GetByBagNumberForClientAsync(
        string bagNumber,
        string clientId,
        CancellationToken cancellationToken);

    Task<Result<BagRegistrationDocument>> UpdateAsync(
        BagRegistrationDocument document,
        CancellationToken cancellationToken);

    Task<Result<BagRegistrationDocument>> UpdateWithOutboxAsync(
        BagRegistrationDocument document,
        OutboxMessageDocument outboxMessage,
        CancellationToken cancellationToken);
}