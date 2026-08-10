namespace Nokas.CashMgmt.Shared.Contracts;

// Property names intentionally mirror the columns returned by dbo.api_CancelBagIfRegistered
// (SQL/api_CancelBagIfRegistered.sql) so Dapper can bind them by name on the Function side,
// and the same shape round-trips over JSON to the API unchanged.
public sealed class BagStatusRecord
{
    public string? intInnkastnr { get; init; }
    public string? vchCustomer_id { get; init; }
    public string? vchName { get; init; }
    public string? dblAmount { get; init; }
    public string? vchLocation { get; init; }
    public DateTime? dtmRegDate { get; init; }
    public DateTime? dtmTransportFetch { get; init; }
    public int? intStatus { get; init; }
}
