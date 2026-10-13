using System.ComponentModel.DataAnnotations;

namespace Nokas.CashMgmt.No.Api.Configuration;

public sealed class BagStatusFunctionOptions
{
    [Required]
    public required string BaseUrl { get; init; }

    [Required]
    public required string FunctionKey { get; init; }
}