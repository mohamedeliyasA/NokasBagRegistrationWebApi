using Nokas.CashMgmt.No.Api.Repositories;
using Nokas.CashMgmt.No.Shared.Documents;

namespace Nokas.CashMgmt.No.Api.Tests.Controllers;

public sealed class FakeCountingResultRepository : ICountingResultRepository
{
    public CountingResultPage QueryResult { get; set; } = new([], null);
    public CountingResultDocument? SingleResult { get; set; }
    public DateTime? DataAsOfResult { get; set; }
    public CountingResultQueryOptions? LastQuery { get; set; }
    public string? LastNativeContinuationToken { get; set; }

    public Task<CountingResultPage> QueryAsync(string clientId, CountingResultQueryOptions query, string? nativeContinuationToken, CancellationToken cancellationToken)
    {
        LastQuery = query;
        LastNativeContinuationToken = nativeContinuationToken;
        return Task.FromResult(QueryResult);
    }

    public Task<CountingResultDocument?> GetLatestByBagNumberAsync(string clientId, string bagNumber, CancellationToken cancellationToken) =>
        Task.FromResult(SingleResult);

    public Task<DateTime?> GetDataAsOfAsync(CancellationToken cancellationToken) => Task.FromResult(DataAsOfResult);
}
