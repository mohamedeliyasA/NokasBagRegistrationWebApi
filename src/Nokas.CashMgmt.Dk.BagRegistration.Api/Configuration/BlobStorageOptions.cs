using System.ComponentModel.DataAnnotations;

namespace Nokas.CashMgmt.Dk.BagRegistration.Api.Configuration;

public sealed class BlobStorageOptions
{
    [Required]
    public required string AccountUri { get; init; }

    [Required]
    public required string ContainerName { get; init; }
}
