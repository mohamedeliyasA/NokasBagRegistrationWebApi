namespace Nokas.CashMgmt.Dk.BagRegistration.Api.Repositories;

public interface IDkCustomerDirectoryRepository
{
    /// <summary>
    /// Human-readable customer name for a client id, or null if the client id has not been
    /// added to the directory yet. Never throws - a lookup failure or missing entry must not
    /// block a registration or a ServiceNow forward, only omit the enrichment.
    /// </summary>
    Task<string?> GetCustomerNameAsync(string clientId, CancellationToken cancellationToken);
}
