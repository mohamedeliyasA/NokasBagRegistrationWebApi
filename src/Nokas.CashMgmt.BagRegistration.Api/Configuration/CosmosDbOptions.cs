using System.ComponentModel.DataAnnotations;

namespace Nokas.CashMgmt.BagRegistration.Api.Configuration;

public sealed class CosmosDbOptions
{
    [Required]
    public required string AccountEndpoint { get; init; }

    [Required]
    public required string DatabaseName { get; init; }

    [Required]
    public required string BagRegistrationContainerName { get; init; }

    [Required]
    public required string BagNumberCacheContainerName { get; init; }

    [Required]
    public required string CountingResultContainerName { get; init; }
}