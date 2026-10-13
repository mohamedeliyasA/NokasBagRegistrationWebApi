using System.ComponentModel.DataAnnotations;

namespace Nokas.CashMgmt.Dk.Shared.Configuration;

/// <summary>
/// Just the Cosmos coordinates the counting-result sync needs. Deliberately narrower than the
/// API's own options: the sync has no business knowing about the registration or customer
/// containers, and both apps bind this from the same CosmosDb configuration keys.
/// </summary>
public sealed class DkCosmosOptions
{
    [Required]
    public required string AccountEndpoint { get; init; }

    [Required]
    public required string DatabaseName { get; init; }

    [Required]
    public required string CountingResultContainerName { get; init; }

    [Required]
    public required string KardexMapContainerName { get; init; }
}
