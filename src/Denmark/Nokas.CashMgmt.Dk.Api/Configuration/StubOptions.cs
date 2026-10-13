using System.ComponentModel.DataAnnotations;

namespace Nokas.CashMgmt.Dk.Api.Configuration;

// Tuning for the registration journal and validation.
public sealed class StubOptions
{
    /// <summary>Description returned on a successful GET.</summary>
    [Required]
    public required string FoundDescription { get; init; }

    // A payload larger than this is archived to Blob only; Cosmos keeps the metadata
    // and a marker instead of the body, so an oversized post cannot fail the request
    // on the 2 MB Cosmos document limit.
    [Range(1024, 1_800_000)]
    public int MaxCosmosPayloadBytes { get; init; } = 1_000_000;

    // When true, totalOrderAmount must equal the sum of bagItemsLC face values and a
    // mismatch is rejected; when false it is reported as a non-blocking warning. Starts
    // permissive so a partner integrating for the first time is not blocked by a rule
    // that has not been agreed with them.
    public bool EnforceDenominationTotal { get; init; }
}
