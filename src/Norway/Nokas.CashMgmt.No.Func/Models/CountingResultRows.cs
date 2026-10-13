namespace Nokas.CashMgmt.No.Func.Models;

/// <summary>Property names match the stored procedure's output column aliases for Dapper
/// name-binding. Declared/counted amounts are double, not decimal - the underlying columns are
/// SQL float.</summary>
public sealed record CountingResultChangeRow(
    int NattsafeRecordId,
    string BagNumber,
    string? CustomerNumber,
    string? MyReference,
    double? DeclaredAmount,
    double? CountedAmount,
    DateTime? CountedAt,
    DateTime? TransportFetchedAt,
    DateTime? RegisteredAt,
    int? StatusCode,
    DateTime LastUpdatedAt);

/// <summary>Compact status-change history for a single bag.</summary>
public sealed record CountingResultHistoryRow(
    int NattsafeRecordId,
    int StatusCode,
    DateTime ChangedAt);

/// <summary>Property names match the mapping table's own column names for Dapper name-binding.</summary>
public sealed record CustomerMappingRow(
    string ClientID,
    string NokasCustomerNumber,
    string CustomerRefNumber,
    DateTime? BackfillFromDate,
    DateTime CreatedAt);
