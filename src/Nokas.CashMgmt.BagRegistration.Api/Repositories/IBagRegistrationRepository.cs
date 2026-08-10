using Nokas.CashMgmt.BagRegistration.Api.Documents;
using Nokas.CashMgmt.Shared.Results;

namespace Nokas.CashMgmt.BagRegistration.Api.Repositories;

public interface IBagRegistrationRepository
{
    Task<Result<BagRegistrationDocument>> CreateAsync(
        BagRegistrationDocument document,
        OutboxMessageDocument outboxMessage,
        CancellationToken cancellationToken);
}