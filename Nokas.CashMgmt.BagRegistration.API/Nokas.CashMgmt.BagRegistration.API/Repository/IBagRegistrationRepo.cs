using Nokas.CashMgmt.BagRegistration.API.Models;

namespace Nokas.CashMgmt.BagRegistration.API.Repository
{
    public interface IBagRegistrationRepo
    {
        Task<BagRegistrationRequest?> GetBagByNumberForClientAsync(string bagNumber, string clientId, CancellationToken cancellationToken = default);

        /// <summary>Writes the bag document and its outbox event atomically (Cosmos transactional batch).</summary>
        Task<BagRegistrationRequest> CreateBagRegistration(BagRegistrationRequest bagRegistrationRequest, OutboxMessage outboxMessage, CancellationToken cancellationToken = default);

        Task<BagRegistrationRequest> UpdateBagRegistration(BagRegistrationRequest bagUpdate, CancellationToken cancellationToken = default);

        /// <summary>Updates the bag document and writes its outbox event atomically (Cosmos transactional batch).</summary>
        Task UpdateBagRegistrationWithOutboxAsync(BagRegistrationRequest bagUpdate, OutboxMessage outboxMessage, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<OutboxMessage>> GetPendingOutboxMessagesAsync(int maxItems, CancellationToken cancellationToken = default);

        Task MarkOutboxMessagePublishedAsync(string outboxId, string clientId, CancellationToken cancellationToken = default);

        Task EnsureClientInDirectoryAsync(string clientId, string customerNumber, string customerName);

    }
}
