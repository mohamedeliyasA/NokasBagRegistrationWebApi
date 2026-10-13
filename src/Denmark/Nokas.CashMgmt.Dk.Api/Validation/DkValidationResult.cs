namespace Nokas.CashMgmt.Dk.Api.Validation;

/// <summary>One failed rule, named by its JSON path so the partner can find it.</summary>
public sealed record DkValidationFailure(string Field, string Message);

/// <summary>The fields lifted out of a payload that passed validation.</summary>
public sealed record DkBagRegistrationFields(
    string CustomerRefNumber,
    string CashRegisterId,
    string MessageId,
    string BagNumber,
    DateOnly RegistrationDate,
    decimal TotalOrderAmount,
    IReadOnlyDictionary<string, int>? BagItemsLC,
    // Optional free-form partner metadata. Null when none was sent.
    IReadOnlyDictionary<string, string>? MetaData);

public sealed class DkValidationResult
{
    public required IReadOnlyList<DkValidationFailure> Failures { get; init; }

    /// <summary>Non-blocking observations. Present even on a fully valid payload.</summary>
    public required IReadOnlyList<string> Warnings { get; init; }

    /// <summary>Sum of face value x quantity, when bagItemsLC parsed. Null otherwise.</summary>
    public decimal? DenominationTotal { get; init; }

    /// <summary>Populated only when <see cref="IsValid"/> is true.</summary>
    public DkBagRegistrationFields? Fields { get; init; }

    public bool IsValid => Failures.Count == 0;

    public string JoinedMessages => string.Join(" | ", Failures.Select(f => $"{f.Field}: {f.Message}"));
}
