using System.ComponentModel.DataAnnotations;

namespace Nokas.CashMgmt.No.Api.Configuration;

public sealed class ServiceBusOptions
{
    [Required]
    public required string FullyQualifiedNamespace { get; init; }

    [Required]
    public required string TopicName { get; init; }
}