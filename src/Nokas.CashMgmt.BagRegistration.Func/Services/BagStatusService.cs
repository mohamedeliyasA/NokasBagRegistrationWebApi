using Dapper;
using Microsoft.Data.SqlClient;

namespace Nokas.CashMgmt.BagRegistration.Func.Services;

public sealed class BagStatusService : IBagStatusService
{
    private readonly string _cacheDbConnectionString;

    public BagStatusService()
    {
        _cacheDbConnectionString = Environment.GetEnvironmentVariable("NokasMwCacheDBConnString")
            ?? throw new InvalidOperationException("Missing 'NokasMwCacheDBConnString' environment variable.");
    }

    public async Task<bool> ValidateBagNumberAsync(string bagNumber, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_cacheDbConnectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = "SELECT TOP 1 1 FROM dbo.Registration WHERE BagNumber = @BagNumber";
        var result = await connection.ExecuteScalarAsync<int?>(sql, new { BagNumber = bagNumber });

        return result.HasValue;
    }

    public async Task<IReadOnlyList<(string BagNumber, DateTime CreatedAt)>> GetBagNumbersCreatedAfterAsync(
        DateTime sinceUtc, int maxRows, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_cacheDbConnectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT TOP (@MaxRows) BagNumber, CreatedAt
            FROM dbo.Registration
            WHERE CreatedAt > @SinceUtc AND BagNumber IS NOT NULL AND LTRIM(RTRIM(BagNumber)) <> ''
            ORDER BY CreatedAt ASC
            """;

        var rows = await connection.QueryAsync<RegistrationRow>(sql, new { SinceUtc = sinceUtc, MaxRows = maxRows });

        return rows.Select(r => (r.BagNumber, r.CreatedAt)).ToList();
    }

    private sealed record RegistrationRow(string BagNumber, DateTime CreatedAt);
}