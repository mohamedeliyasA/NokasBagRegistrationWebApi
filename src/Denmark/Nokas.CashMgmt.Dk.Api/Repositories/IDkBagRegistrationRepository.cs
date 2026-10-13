using Nokas.CashMgmt.Dk.Api.Documents;
using Nokas.CashMgmt.Dk.Shared.Documents;

namespace Nokas.CashMgmt.Dk.Api.Repositories;

public interface IDkBagRegistrationRepository
{
    Task SaveAsync(DkBagRegistrationDocument document, CancellationToken cancellationToken);

    /// <summary>
    /// Most recent accepted registration for a bag number within the caller's partition.
    /// A bag number is not unique in the journal, so the newest accepted submission wins.
    /// </summary>
    Task<DkBagRegistrationDocument?> GetLatestByBagNumberAsync(string bagNumber, string clientId, CancellationToken cancellationToken);

}
