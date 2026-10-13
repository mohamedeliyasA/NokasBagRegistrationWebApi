using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Nokas.CashMgmt.No.Func.Models;

namespace Nokas.CashMgmt.No.Func.Services;

/// <summary>
/// Read-only access to the two on-prem SQL sources the counting-result sync needs: the customer
/// mapping table (BizTalk_Helper) and the change-feed stored procedure (Nattsafe).
/// </summary>
public sealed class CountingResultDataService : ICountingResultDataService
{
    private readonly string _nattsafeConnectionString;
    private readonly string _cmsMappingConnectionString;

    public CountingResultDataService()
    {
        _nattsafeConnectionString = Environment.GetEnvironmentVariable("NattsafeSQLDBConnString")
            ?? throw new InvalidOperationException("Missing 'NattsafeSQLDBConnString' environment variable.");
        _cmsMappingConnectionString = Environment.GetEnvironmentVariable("CmsMappingConnString")
            ?? throw new InvalidOperationException("Missing 'CmsMappingConnString' environment variable.");
    }

    public async Task<IReadOnlyList<CustomerMappingRow>> GetCustomerMappingsAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_cmsMappingConnectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT ClientID, NokasCustomerNumber, CustomerRefNumber, BackfillFromDate, CreatedAt
            FROM dbo.NokasExternalCustomerMapping
            WHERE ClientID IS NOT NULL AND NokasCustomerNumber IS NOT NULL
            """;

        var rows = await connection.QueryAsync<CustomerMappingRow>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<(IReadOnlyList<CountingResultChangeRow> Changes, IReadOnlyList<CountingResultHistoryRow> History)> FetchChangesAsync(
        string mode, DateTime sinceDate, IReadOnlyCollection<string>? customerNumbers, int pageSize, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_nattsafeConnectionString);
        await connection.OpenAsync(cancellationToken);

        var parameters = new DynamicParameters();
        parameters.Add("@Mode", mode, DbType.String, size: 10);
        parameters.Add("@SinceDate", sinceDate, DbType.DateTime2);
        parameters.Add("@CustomerNumbers", customerNumbers is { Count: > 0 } ? string.Join(",", customerNumbers) : null, DbType.String, size: -1);
        parameters.Add("@PageSize", pageSize, DbType.Int32);

        var commandDefinition = new CommandDefinition(
            "dbo.api_CountingResultFetchChanges", parameters, commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken);

        using var multi = await connection.QueryMultipleAsync(commandDefinition);
        var changes = (await multi.ReadAsync<CountingResultChangeRow>()).ToList();
        var history = (await multi.ReadAsync<CountingResultHistoryRow>()).ToList();

        return (changes, history);
    }
}
