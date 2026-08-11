using Nokas.CashMgmt.BagRegistration.Func.Models;

namespace Nokas.CashMgmt.BagRegistration.Func.Services;

public interface ICountingResultDataService
{
    Task<IReadOnlyList<CustomerMappingRow>> GetCustomerMappingsAsync(CancellationToken cancellationToken);

    Task<(IReadOnlyList<CountingResultChangeRow> Changes, IReadOnlyList<CountingResultHistoryRow> History)> FetchChangesAsync(
        string mode, DateTime sinceDate, IReadOnlyCollection<string>? customerNumbers, int pageSize, CancellationToken cancellationToken);
}
