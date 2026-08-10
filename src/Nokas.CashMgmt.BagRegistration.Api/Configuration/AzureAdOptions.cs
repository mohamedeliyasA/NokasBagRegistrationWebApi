using System.ComponentModel.DataAnnotations;

namespace Nokas.CashMgmt.BagRegistration.Api.Configuration;

public sealed class AzureAdOptions
{
    [Required]
    public required string TenantId { get; init; }
    [Required]
    public required string Authority { get; init; }

    [Required]
    public required string Audience { get; init; }
}