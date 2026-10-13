namespace Nokas.CashMgmt.No.Api.Models;

public sealed class CountingResultsQueryRequest
{
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public string DateType { get; init; } = "counted";
    public string? Status { get; init; }
    public string? CustomerRefNumber { get; init; }
    public int? PageSize { get; init; }
    public string? ContinuationToken { get; init; }
}
