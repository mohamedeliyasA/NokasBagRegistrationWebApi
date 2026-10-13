using System.ComponentModel.DataAnnotations;

namespace Nokas.CashMgmt.No.Api.Configuration;

public sealed class CountingResultsOptions
{
    [Required]
    public required QuerySettings Query { get; init; }

    public sealed class QuerySettings
    {
        [Range(1, 366)]
        public int MaxSpanDays { get; init; } = 92;

        [Range(1, 1000)]
        public int MaxPageSize { get; init; } = 500;

        [Range(1, 1000)]
        public int DefaultPageSize { get; init; } = 100;
    }
}
