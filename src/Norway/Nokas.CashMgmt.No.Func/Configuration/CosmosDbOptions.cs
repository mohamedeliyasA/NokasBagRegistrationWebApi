namespace Nokas.CashMgmt.No.Func.Configuration;

public sealed class CosmosDbOptions
{
    public required string AccountEndpoint { get; init; }
    public required string DatabaseName { get; init; }
    public required string BagNumberCacheContainerName { get; init; }
    public required string CountingResultContainerName { get; init; }
}
