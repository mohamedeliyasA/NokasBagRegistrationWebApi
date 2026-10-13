namespace Nokas.CashMgmt.Dk.Shared.Services;

/// <summary>
/// Maps ServiceNow's <c>state</c> to the bucket vocabulary DK's API exposes. The full state list
/// was supplied by ServiceNow on 2026-09-23; the buckets deliberately match Norway's
/// (registered | in-progress | picked-up | counted | cancelled | error) so a partner integrating
/// with both markets learns one vocabulary.
///
/// Rows carry the state NAME; the two numeric codes seen on the wire are accepted as well.
/// Anything unrecognised maps to "unknown" rather than being guessed at or dropped - the raw
/// value is still stored, so a new state shows up in support tooling instead of vanishing.
/// </summary>
public static class DkCountingResultStatusMapper
{
    public const string Registered = "registered";
    public const string InProgress = "in-progress";
    public const string PickedUp = "picked-up";
    public const string Counted = "counted";
    public const string Cancelled = "cancelled";
    public const string Error = "error";
    public const string Unknown = "unknown";

    public static readonly IReadOnlyList<string> ValidBuckets =
        [Registered, InProgress, PickedUp, Counted, Cancelled, Error, Unknown];

    private static readonly Dictionary<string, string> RawToBucket = new(StringComparer.OrdinalIgnoreCase)
    {
        // Registered with NOKAS, nothing has happened to the bag yet.
        ["Draft"] = Registered,
        ["New"] = Registered,
        ["1"] = Registered,
        ["Registered"] = Registered,

        // Collected from the customer and on its way.
        ["Ordered"] = PickedUp,
        ["Packed"] = PickedUp,
        ["In transit"] = PickedUp,

        // With NOKAS, not yet counted.
        ["In cash center"] = InProgress,
        ["In cash depot"] = InProgress,
        ["Work in Progress"] = InProgress,
        ["On Hold"] = InProgress,

        // Counted; the counted amount is meaningful from here on.
        ["Counted"] = Counted,
        ["Paid out"] = Counted,
        ["10"] = Counted,
        ["Closed"] = Counted,
        ["Completed"] = Counted,

        ["Cancelled"] = Cancelled,
        ["Error"] = Error
    };

    public static string ToBucket(string? rawState) =>
        rawState is not null && RawToBucket.TryGetValue(rawState.Trim(), out var bucket) ? bucket : Unknown;
}
