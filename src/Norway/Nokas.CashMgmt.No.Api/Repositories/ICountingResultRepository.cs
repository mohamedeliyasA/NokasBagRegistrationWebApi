using Nokas.CashMgmt.No.Shared.Documents;

namespace Nokas.CashMgmt.No.Api.Repositories;

public sealed record CountingResultQueryOptions(
    DateTime FromDate,
    DateTime ToDate,
    string DateType,
    string? StatusBucket,
    string? CustomerRefNumber,
    int PageSize);

public sealed record CountingResultPage(IReadOnlyList<CountingResultDocument> Items, string? ContinuationToken);

public interface ICountingResultRepository
{
    Task<CountingResultPage> QueryAsync(string clientId, CountingResultQueryOptions query, string? nativeContinuationToken, CancellationToken cancellationToken);

    Task<CountingResultDocument?> GetLatestByBagNumberAsync(string clientId, string bagNumber, CancellationToken cancellationToken);

    Task<DateTime?> GetDataAsOfAsync(CancellationToken cancellationToken);
}
