using System.ComponentModel.DataAnnotations;

namespace Nokas.CashMgmt.Dk.Api.Configuration;

public sealed class CosmosDbOptions
{
    [Required]
    public required string AccountEndpoint { get; init; }

    [Required]
    public required string DatabaseName { get; init; }

    [Required]
    public required string RegistrationContainerName { get; init; }

    [Required]
    public required string CustomerDirectoryContainerName { get; init; }

    [Required]
    public required string KardexMapContainerName { get; init; }

    [Required]
    public required string CountingResultContainerName { get; init; }
}
