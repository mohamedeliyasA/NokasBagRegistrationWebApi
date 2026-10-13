using System.ComponentModel.DataAnnotations;

namespace Nokas.CashMgmt.Dk.Api.Configuration;

public sealed class AzureAdOptions
{
    [Required]
    public required string TenantId { get; init; }

    [Required]
    public required string Authority { get; init; }

    [Required]
    public required string Audience { get; init; }

    // Configurable rather than compiled in: the Danish app role is provisioned separately
    // from this deployment, so the required role can be pointed at a different value
    // without a rebuild.
    [Required]
    public required string RequiredRole { get; init; }
}
