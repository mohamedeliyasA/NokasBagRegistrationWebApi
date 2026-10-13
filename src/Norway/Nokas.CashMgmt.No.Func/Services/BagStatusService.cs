using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Nokas.CashMgmt.No.Shared.Contracts;

namespace Nokas.CashMgmt.No.Func.Services;

public sealed class BagStatusService : IBagStatusService
{
    private readonly string _cacheDbConnectionString;
    private readonly string _nattsafeConnectionString;

    public BagStatusService()
    {
        _cacheDbConnectionString = Environment.GetEnvironmentVariable("NokasMwCacheDBConnString")
            ?? throw new InvalidOperationException("Missing 'NokasMwCacheDBConnString' environment variable.");
        _nattsafeConnectionString = Environment.GetEnvironmentVariable("NattsafeSQLDBConnString")
            ?? throw new InvalidOperationException("Missing 'NattsafeSQLDBConnString' environment variable.");
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

    public async Task<BagStatusRecord?> GetStatusAsync(string bagNumber, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_nattsafeConnectionString);
        await connection.OpenAsync(cancellationToken);

        var parameters = new DynamicParameters();
        parameters.Add("@BagNumber", bagNumber, DbType.String, ParameterDirection.Input, size: 50);
        parameters.Add("@OutStatusCode", dbType: DbType.Int32, direction: ParameterDirection.Output);

        var commandDefinition = new CommandDefinition(
            "dbo.api_CancelBagIfRegistered", parameters, commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken);

        var result = await connection.QueryFirstOrDefaultAsync<BagStatusRecord>(commandDefinition);
        var outStatusCode = parameters.Get<int?>("@OutStatusCode") ?? -1;

        return outStatusCode switch
        {
            1 => null,
            0 or 2 or 3 => result,
            _ => throw new InvalidOperationException($"Unexpected OutStatusCode {outStatusCode} from api_CancelBagIfRegistered.")
        };
    }
}