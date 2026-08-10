using System.ComponentModel.DataAnnotations;

namespace Nokas.CashMgmt.BagRegistration.Api.Configuration;

public sealed class BlobStorageOptions
{
    [Required]
    public required string AccountUri { get; init; }

    [Required]
    public required string ContainerName { get; init; }
}
