using Dapper;
using Microsoft.Data.SqlClient;
using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace Nokas.CashMgmt.BagReg.Func
{
    public class BagStatusService : IBagStatusService
    {
        private readonly string _connString;
        private readonly string _portalDBString;

        public BagStatusService()
        {
            // Read from environment variables (Function App → Configuration → Application settings)
            _connString = Environment.GetEnvironmentVariable("NattsafeSQLDBConnString");
            _portalDBString = Environment.GetEnvironmentVariable("NokasMwCacheDBConnString");

            if (string.IsNullOrWhiteSpace(_connString))
                throw new InvalidOperationException("Missing DB connection string in environment variable 'NattsafeSQLDBConnString'.");
        }

        public async Task<BagStatusRecord?> GetStatusAsync(string bagNumber, CancellationToken ct = default)
        {
            var databaseName = new SqlConnectionStringBuilder(_connString).InitialCatalog;
            try
            {
                await using var conn = new SqlConnection(_connString);
                await conn.OpenAsync(ct);

                var parameters = new DynamicParameters();
                parameters.Add("@BagNumber", bagNumber, DbType.String, ParameterDirection.Input, size: 50);
                parameters.Add("@OutStatusCode", dbType: DbType.Int32, direction: ParameterDirection.Output);

                // Dapper will map the first row returned by the proc to BagStatusRecord
                var result = await conn.QueryFirstOrDefaultAsync<BagStatusRecord>(
                    "dbo.api_CancelBagIfRegistered",
                    parameters,
                    commandType: CommandType.StoredProcedure
                );

                // read output params
                var outStatus = parameters.Get<int?>("@OutStatusCode") ?? -1;


                switch (outStatus)
                {
                    case 0:
                        return result;

                    case 1: // not found
                        return null;
                    case 2:  // already cancelled
                        return result;
                    case 3:
                        return result;
                    case 4:
                        throw new Exception($"Database procedure error");
                    default:
                        // unexpected code -> throw
                        throw new Exception($"Unexpected status code from proc: {outStatus}");
                }


            }
            catch (SqlException ex)
            {
                throw new BagStatusSqlException(ex, databaseName);
            }
        }

        public async Task<BagCountingResultRecord?> GetCountingResultAsync(string bagNumber, CancellationToken ct = default)
        {
            var databaseName = new SqlConnectionStringBuilder(_connString).InitialCatalog;
            try
            {
                await using var conn = new SqlConnection(_connString);
                await conn.OpenAsync(ct);

                const string sql = @"
                    SELECT 
                        intStatus, 
                        dblAmount, 
                        dblControlledAmount, 
                        intInnkastnr, 
                        vchCustomer_id, 
                        dtmControlledAt,
                        dtmTransportFetch
                    FROM Nattsafearkiv (NOLOCK)
                    WHERE intInnkastnr = @BagNumber";

                var result = await conn.QueryFirstOrDefaultAsync<BagCountingResultRecord>(
                    sql,
                    new { BagNumber = bagNumber },
                    commandType: CommandType.Text
                );

                return result;
            }
            catch (SqlException ex)
            {
                throw new BagStatusSqlException(ex, databaseName);
            }
        }

        public async Task<bool> ValidateBagNumber(string bagNumber, CancellationToken ct = default)
        {
            var databaseName = new SqlConnectionStringBuilder(_portalDBString).InitialCatalog;
            try
            {
                await using var conn = new SqlConnection(_portalDBString);
                await conn.OpenAsync(ct);

                const string sql = @"
            SELECT TOP 1 1 
            FROM dbo.Registration 
            WHERE BagNumber = @BagNumber
        ";

                var result = await conn.ExecuteScalarAsync<int?>(
                    sql,
                    new { BagNumber = bagNumber },
                    commandType: CommandType.Text
                );

                return result.HasValue;
            }
            catch (SqlException ex)
            {
                throw new BagStatusSqlException(ex, databaseName);
            }
        }
    }
}

public class BagStatusRecord
{
    public string? intInnkastnr { get; set; }
    public string? vchCustomer_id { get; set; }
    public string? vchName { get; set; }

    public string? dblAmount { get; set; }

    public string? vchLocation { get; set; }
    public DateTime? dtmRegDate { get; set; }
    public DateTime? dtmTransportFetch { get; set; }
    public int? intStatus { get; set; }
}

public class BagCountingResultRecord
{
    public int? intStatus { get; set; }
    public decimal? dblAmount { get; set; }
    public decimal? dblControlledAmount { get; set; }
    public string? intInnkastnr { get; set; }
    public string? vchCustomer_id { get; set; }
    public DateTime? dtmControlledAt { get; set; }

    public DateTime? dtmTransportFetch { get; set; }
}


public class BagStatusSqlException : Exception
{
    public string Server { get; }
    public string Database { get; }
    public int Number { get; }
    public string Procedure { get; }
    public int Line { get; }

    public BagStatusSqlException(SqlException sqlEx, string database)
        : base(sqlEx.Message, sqlEx)
    {
        Server = sqlEx.Server;
        Database = database;
        Number = sqlEx.Number;
        Procedure = sqlEx.Procedure;
        Line = sqlEx.LineNumber;
    }
}



