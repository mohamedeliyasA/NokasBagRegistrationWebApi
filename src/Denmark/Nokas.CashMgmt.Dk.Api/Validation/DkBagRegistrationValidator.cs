using System.Globalization;
using System.Text.Json;

namespace Nokas.CashMgmt.Dk.Api.Validation;

/// <summary>
/// Validates the Danish bag registration contract. A pure function over the parsed JSON:
/// no I/O, no configuration beyond the flags passed in, so every rule is unit-testable.
///
/// Validation runs against the raw JSON rather than a bound model so that unknown fields,
/// wrong JSON types, and malformed bodies all produce partner-readable messages naming the
/// offending JSON path, instead of the serializer's .NET type errors.
/// </summary>
public static class DkBagRegistrationValidator
{
    private const string Root = "bagRegistration";
    private const string MetaRoot = "metaData";

    // Free-form by design: a partner may send whatever identifiers their own system uses
    // (store id, company id, till id). Bounded so it cannot become a payload channel.
    private const int MaxMetaEntries = 20;
    private const int MaxMetaKeyLength = 50;
    private const int MaxMetaValueLength = 200;

    private const int MaxCustomerRefNumberLength = 50;
    private const int MaxCashRegisterIdLength = 50;
    private const int MaxBagNumberLength = 20;

    private static readonly string[] AllowedDetailFields =
    [
        "customerRefNumber", "cashRegisterId", "messageId",
        "bagNumber", "registrationDate", "totalOrderAmount", "bagItemsLC"
    ];

    /// <param name="body">The raw request body.</param>
    /// <param name="enforceDenominationTotal">
    /// When true, a mismatch between totalOrderAmount and the denomination sum is an error;
    /// otherwise it is reported as a warning only.
    /// </param>
    public static DkValidationResult Validate(string body, bool enforceDenominationTotal)
    {
        var failures = new List<DkValidationFailure>();
        var warnings = new List<string>();

        JsonElement root;
        try
        {
            using var document = JsonDocument.Parse(body);
            root = document.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            return Fail("body", $"Request body is not valid JSON. {ex.Message}");
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            return Fail("body", "Request body must be a JSON object.");
        }

        // Root may contain bagRegistration and nothing else.
        foreach (var property in root.EnumerateObject())
        {
            if (property.Name != Root && property.Name != MetaRoot)
            {
                failures.Add(new DkValidationFailure(property.Name,
                    $"Unknown field. Allowed root fields are: {Root}, {MetaRoot}."));
            }
        }

        if (!root.TryGetProperty(Root, out var detail))
        {
            failures.Add(new DkValidationFailure(Root, "Required field is missing."));
            return new DkValidationResult { Failures = failures, Warnings = warnings };
        }

        if (detail.ValueKind != JsonValueKind.Object)
        {
            failures.Add(new DkValidationFailure(Root, "Must be a JSON object."));
            return new DkValidationResult { Failures = failures, Warnings = warnings };
        }

        foreach (var property in detail.EnumerateObject())
        {
            if (!AllowedDetailFields.Contains(property.Name, StringComparer.Ordinal))
            {
                failures.Add(new DkValidationFailure($"{Root}.{property.Name}",
                    $"Unknown field. Allowed fields are: {string.Join(", ", AllowedDetailFields)}."));
            }
        }

        var customerRefNumber = RequireString(detail, "customerRefNumber", MaxCustomerRefNumberLength, failures);
        var cashRegisterId = RequireString(detail, "cashRegisterId", MaxCashRegisterIdLength, failures);
        var messageId = ValidateMessageId(detail, failures);
        var bagNumber = ValidateBagNumber(detail, failures);
        var registrationDate = ValidateRegistrationDate(detail, failures);
        var totalOrderAmount = ValidateTotalOrderAmount(detail, failures);
        var bagItemsValid = TryValidateBagItems(detail, failures, out var bagItems);
        var metaData = ValidateMetaData(root, failures);

        // bagItemsLC is optional. The declared total is only cross-checked when a breakdown
        // was actually supplied - there is nothing to compare it against otherwise.
        decimal? denominationTotal = null;
        if (bagItemsValid && bagItems is { Count: > 0 })
        {
            denominationTotal = bagItems.Sum(item => DkDenominations.FaceValue[item.Key] * item.Value);

            if (totalOrderAmount is not null && denominationTotal != totalOrderAmount)
            {
                var message =
                    $"totalOrderAmount ({totalOrderAmount.Value.ToString(CultureInfo.InvariantCulture)}) does not equal the " +
                    $"sum of bagItemsLC ({denominationTotal.Value.ToString(CultureInfo.InvariantCulture)}). " +
                    "Each bagItemsLC value is a quantity of that denomination, not an amount.";

                if (enforceDenominationTotal)
                {
                    failures.Add(new DkValidationFailure($"{Root}.totalOrderAmount", message));
                }
                else
                {
                    warnings.Add(message);
                }
            }
        }

        if (failures.Count > 0)
        {
            return new DkValidationResult
            {
                Failures = failures,
                Warnings = warnings,
                DenominationTotal = denominationTotal
            };
        }

        return new DkValidationResult
        {
            Failures = failures,
            Warnings = warnings,
            DenominationTotal = denominationTotal,
            Fields = new DkBagRegistrationFields(
                customerRefNumber!, cashRegisterId!, messageId!, bagNumber!,
                registrationDate!.Value, totalOrderAmount!.Value, bagItems, metaData)
        };
    }

    // Optional, and open on purpose: any key/value pair the partner wants echoed through to
    // NOKAS. Values must be strings so the meaning never depends on how a number was written,
    // matching the rest of this contract.
    private static IReadOnlyDictionary<string, string>? ValidateMetaData(
        JsonElement root, List<DkValidationFailure> failures)
    {
        if (!root.TryGetProperty(MetaRoot, out var meta) || meta.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (meta.ValueKind != JsonValueKind.Object)
        {
            failures.Add(new DkValidationFailure(MetaRoot,
                $"Must be a JSON object of name/value pairs, but was {DescribeKind(meta.ValueKind)}."));
            return null;
        }

        var entries = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var property in meta.EnumerateObject())
        {
            var path = $"{MetaRoot}.{property.Name}";

            if (entries.Count >= MaxMetaEntries)
            {
                failures.Add(new DkValidationFailure(MetaRoot, $"Must not contain more than {MaxMetaEntries} entries."));
                break;
            }

            if (property.Name.Length == 0 || property.Name.Length > MaxMetaKeyLength)
            {
                failures.Add(new DkValidationFailure(path, $"Field names must be 1-{MaxMetaKeyLength} characters."));
                continue;
            }

            if (property.Value.ValueKind != JsonValueKind.String)
            {
                failures.Add(new DkValidationFailure(path,
                    $"Must be a JSON string (quoted), but was {DescribeKind(property.Value.ValueKind)}."));
                continue;
            }

            var value = property.Value.GetString() ?? string.Empty;
            if (value.Length > MaxMetaValueLength)
            {
                failures.Add(new DkValidationFailure(path, $"Must not be longer than {MaxMetaValueLength} characters."));
                continue;
            }

            entries[property.Name] = value;
        }

        return entries.Count > 0 ? entries : null;
    }

    private static DkValidationResult Fail(string field, string message) => new()
    {
        Failures = [new DkValidationFailure(field, message)],
        Warnings = []
    };

    // Every field in this contract is a JSON string, including the numeric ones. Accepting a
    // JSON number instead would let a float representation decide an amount.
    private static bool TryReadString(JsonElement parent, string name, List<DkValidationFailure> failures, out string value)
    {
        value = string.Empty;
        var path = $"{Root}.{name}";

        if (!parent.TryGetProperty(name, out var element))
        {
            failures.Add(new DkValidationFailure(path, "Required field is missing."));
            return false;
        }

        if (element.ValueKind != JsonValueKind.String)
        {
            failures.Add(new DkValidationFailure(path,
                $"Must be a JSON string (quoted), but was {DescribeKind(element.ValueKind)}."));
            return false;
        }

        value = element.GetString() ?? string.Empty;
        return true;
    }

    private static string? RequireString(JsonElement parent, string name, int maxLength, List<DkValidationFailure> failures)
    {
        if (!TryReadString(parent, name, failures, out var value))
        {
            return null;
        }

        var path = $"{Root}.{name}";

        if (string.IsNullOrWhiteSpace(value))
        {
            failures.Add(new DkValidationFailure(path, "Must not be empty."));
            return null;
        }

        if (value.Length > maxLength)
        {
            failures.Add(new DkValidationFailure(path, $"Must be at most {maxLength} characters."));
            return null;
        }

        return value;
    }

    private static string? ValidateMessageId(JsonElement parent, List<DkValidationFailure> failures)
    {
        if (!TryReadString(parent, "messageId", failures, out var value))
        {
            return null;
        }

        if (!Guid.TryParse(value, out var parsed))
        {
            failures.Add(new DkValidationFailure($"{Root}.messageId", "Must be a GUID."));
            return null;
        }

        return parsed.ToString();
    }

    private static string? ValidateBagNumber(JsonElement parent, List<DkValidationFailure> failures)
    {
        if (!TryReadString(parent, "bagNumber", failures, out var value))
        {
            return null;
        }

        var path = $"{Root}.bagNumber";

        if (value.Length == 0 || value.Length > MaxBagNumberLength)
        {
            failures.Add(new DkValidationFailure(path, $"Must be 1 to {MaxBagNumberLength} digits."));
            return null;
        }

        if (!value.All(char.IsAsciiDigit))
        {
            failures.Add(new DkValidationFailure(path, "Must contain digits only."));
            return null;
        }

        return value;
    }

    private static DateOnly? ValidateRegistrationDate(JsonElement parent, List<DkValidationFailure> failures)
    {
        if (!TryReadString(parent, "registrationDate", failures, out var value))
        {
            return null;
        }

        if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            failures.Add(new DkValidationFailure($"{Root}.registrationDate", "Must be a calendar date in yyyy-MM-dd format."));
            return null;
        }

        return parsed;
    }

    private static decimal? ValidateTotalOrderAmount(JsonElement parent, List<DkValidationFailure> failures)
    {
        if (!TryReadString(parent, "totalOrderAmount", failures, out var value))
        {
            return null;
        }

        var path = $"{Root}.totalOrderAmount";

        // Called out separately because Danish locale formatting makes this the likeliest
        // mistake, and "1.234" versus "1,234" would otherwise be silently ambiguous.
        if (value.Contains(','))
        {
            failures.Add(new DkValidationFailure(path, "Must use '.' as the decimal separator, not ','."));
            return null;
        }

        if (!decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsed))
        {
            failures.Add(new DkValidationFailure(path, "Must be a positive decimal amount in DKK, for example \"888.50\"."));
            return null;
        }

        if (parsed <= 0)
        {
            failures.Add(new DkValidationFailure(path, "Must be greater than zero."));
            return null;
        }

        var separatorIndex = value.IndexOf('.');
        if (separatorIndex >= 0 && value.Length - separatorIndex - 1 > 2)
        {
            failures.Add(new DkValidationFailure(path, "Must have at most 2 decimal places."));
            return null;
        }

        return parsed;
    }

    /// <summary>
    /// bagItemsLC is optional: a partner may declare only the total. Returns false only when a
    /// breakdown was supplied and is wrong - an absent breakdown is a valid payload, and yields
    /// true with a null dictionary.
    /// </summary>
    private static bool TryValidateBagItems(
        JsonElement parent, List<DkValidationFailure> failures, out IReadOnlyDictionary<string, int>? items)
    {
        const string path = $"{Root}.bagItemsLC";
        items = null;

        if (!parent.TryGetProperty("bagItemsLC", out var element) || element.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            failures.Add(new DkValidationFailure(path, "Must be a JSON object of denomination to quantity."));
            return false;
        }

        var parsed = new Dictionary<string, int>(StringComparer.Ordinal);
        var hadError = false;

        foreach (var property in element.EnumerateObject())
        {
            var itemPath = $"{path}.{property.Name}";

            if (!DkDenominations.IsKnown(property.Name))
            {
                failures.Add(new DkValidationFailure(itemPath,
                    $"Unknown denomination. Allowed denominations are: {DkDenominations.AllowedKeysCsv}."));
                hadError = true;
                continue;
            }

            if (property.Value.ValueKind != JsonValueKind.String)
            {
                failures.Add(new DkValidationFailure(itemPath,
                    $"Quantity must be a JSON string (quoted), but was {DescribeKind(property.Value.ValueKind)}."));
                hadError = true;
                continue;
            }

            var raw = property.Value.GetString() ?? string.Empty;

            // NumberStyles.None rejects signs, separators and whitespace outright.
            if (!int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var quantity))
            {
                failures.Add(new DkValidationFailure(itemPath,
                    "Quantity must be a whole number of notes or coins, for example \"3\"."));
                hadError = true;
                continue;
            }

            parsed[property.Name] = quantity;
        }

        if (hadError)
        {
            return false;
        }

        // An empty object carries no more information than omitting the field, so it is
        // accepted rather than rejected - the field is optional either way.
        items = parsed;
        return true;
    }

    private static string DescribeKind(JsonValueKind kind) => kind switch
    {
        JsonValueKind.Number => "a number",
        JsonValueKind.True or JsonValueKind.False => "a boolean",
        JsonValueKind.Array => "an array",
        JsonValueKind.Object => "an object",
        JsonValueKind.Null => "null",
        _ => "an unsupported value"
    };
}
