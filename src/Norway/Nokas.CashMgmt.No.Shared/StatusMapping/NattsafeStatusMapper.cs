namespace Nokas.CashMgmt.No.Shared.StatusMapping;

public static class NattsafeStatusMapper
{
    public static string ToLabel(int? statusCode, DateTime? transportFetchedAt = null)
    {
        if (transportFetchedAt.HasValue && statusCode is 0 or 1 or 2)
        {
            return "picked-up";
        }

        return statusCode switch
        {
            0 or 1 => "registered",
            2 => "in-progress",
            3 => "picked-up",
            4 => "received by bank",
            5 => "counted",
            8 or 9 or -1 => "error",
            10 => "canceled",
            11 => "not approved",
            12 => "approved by bank",
            13 => "missing",
            _ => "unknown"
        };
    }

    /// <summary>
    /// Query-time fold of the granular status into the 6 values the counting-result API exposes
    /// via its `status` filter. Coarser than, and distinct from, <see cref="ToLabel"/> - do not
    /// use for the registration API's status vocabulary.
    /// </summary>
    public static string ToBucket(int? statusCode, DateTime? transportFetchedAt = null)
    {
        if (transportFetchedAt.HasValue && statusCode is 0 or 1 or 2)
        {
            return "picked-up";
        }

        return statusCode switch
        {
            0 or 1 => "registered",
            2 or 4 or 12 => "in-progress",
            3 => "picked-up",
            5 => "counted",
            10 => "cancelled",
            -1 or 8 or 9 or 11 or 13 => "error",
            _ => "unknown"
        };
    }

    private static readonly IReadOnlyDictionary<string, string[]> GranularLabelsByBucket = new Dictionary<string, string[]>
    {
        ["registered"] = ["registered"],
        ["in-progress"] = ["in-progress", "received by bank", "approved by bank"],
        ["picked-up"] = ["picked-up"],
        ["counted"] = ["counted"],
        ["cancelled"] = ["canceled"],
        ["error"] = ["error", "not approved", "missing"]
    };

    /// <summary>
    /// Reverse of the bucket fold above: the granular <see cref="ToLabel"/> values a bucket covers.
    /// Counting-result documents store the granular label (the transport-fetch override is already
    /// baked into it by <see cref="ToLabel"/>), so filtering by bucket means a WHERE-IN over this set
    /// rather than re-deriving from the raw status code at query time.
    /// </summary>
    public static IReadOnlyList<string> GranularLabelsForBucket(string bucket) =>
        GranularLabelsByBucket.TryGetValue(bucket, out var labels)
            ? labels
            : throw new ArgumentOutOfRangeException(nameof(bucket), bucket, "Unknown counting-result status bucket.");

    public static readonly IReadOnlyList<string> ValidBuckets = ["registered", "in-progress", "picked-up", "counted", "cancelled", "error"];
}
