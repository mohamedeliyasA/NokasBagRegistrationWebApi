namespace Nokas.CashMgmt.Dk.Api.Models;

public sealed class DkCountingResultsQueryRequest
{
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public string DateType { get; init; } = "lastUpdated";
    public string? Status { get; init; }
    public string? CustomerRefNumber { get; init; }
    public int? PageSize { get; init; }
    public string? ContinuationToken { get; init; }
}
