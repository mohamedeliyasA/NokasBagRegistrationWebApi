namespace Nokas.CashMgmt.Dk.BagRegistration.Api.Validation;

/// <summary>
/// The denomination keys accepted in <c>bagItemsLC</c>, and their face value in DKK.
/// Keys are matched exactly (case-sensitive) - the contract defines one spelling each.
/// </summary>
public static class DkDenominations
{
    // DKK_05 is the 50-øre coin, the only sub-krone denomination. The 1000-krone note is
    // deliberately absent: it is being withdrawn from circulation and is not in the contract.
    private static readonly Dictionary<string, decimal> FaceValues = new(StringComparer.Ordinal)
    {
        ["DKK_500"] = 500m,
        ["DKK_200"] = 200m,
        ["DKK_100"] = 100m,
        ["DKK_50"] = 50m,
        ["DKK_20"] = 20m,
        ["DKK_10"] = 10m,
        ["DKK_5"] = 5m,
        ["DKK_2"] = 2m,
        ["DKK_1"] = 1m,
        ["DKK_05"] = 0.50m
    };

    public static IReadOnlyDictionary<string, decimal> FaceValue => FaceValues;

    public static bool IsKnown(string key) => FaceValues.ContainsKey(key);

    /// <summary>Allowed keys, highest face value first, for use in error messages.</summary>
    public static string AllowedKeysCsv { get; } = string.Join(", ", FaceValues.Keys);
}
