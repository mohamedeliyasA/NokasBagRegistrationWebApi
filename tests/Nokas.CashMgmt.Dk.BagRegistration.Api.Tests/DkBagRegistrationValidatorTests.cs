using Nokas.CashMgmt.Dk.BagRegistration.Api.Validation;

namespace Nokas.CashMgmt.Dk.BagRegistration.Api.Tests;

public class DkBagRegistrationValidatorTests
{
    // One of every denomination sums to exactly 888.50, so the reference payload is
    // internally consistent and produces no warning.
    private const string FullDenominations =
        """{ "DKK_500": "1", "DKK_200": "1", "DKK_100": "1", "DKK_50": "1", "DKK_20": "1", "DKK_10": "1", "DKK_5": "1", "DKK_2": "1", "DKK_1": "1", "DKK_05": "1" }""";

    /// <summary>Each argument is a raw JSON token, so tests can inject wrong types as easily as wrong values.</summary>
    private static string Payload(
        string customerRefNumber = "\"5790000123456\"",
        string cashRegisterId = "\"POS-01\"",
        string messageId = "\"0f8fad5b-d9cb-469f-a165-70867728950e\"",
        string bagNumber = "\"3164696800\"",
        string registrationDate = "\"2026-08-17\"",
        string totalOrderAmount = "\"888.50\"",
        string bagItemsLC = FullDenominations,
        string extraDetailFields = "",
        string extraRootFields = "") =>
        $$"""
        {
          "bagRegistration": {
            "customerRefNumber": {{customerRefNumber}},
            "cashRegisterId": {{cashRegisterId}},
            "messageId": {{messageId}},
            "bagNumber": {{bagNumber}},
            "registrationDate": {{registrationDate}},
            "totalOrderAmount": {{totalOrderAmount}},
            "bagItemsLC": {{bagItemsLC}}{{extraDetailFields}}
          }{{extraRootFields}}
        }
        """;

    private static DkValidationResult Validate(string body, bool enforceTotal = false) =>
        DkBagRegistrationValidator.Validate(body, enforceTotal);

    private static void AssertFailsOn(DkValidationResult result, string expectedField)
    {
        Assert.False(result.IsValid);
        Assert.Contains(result.Failures, f => f.Field == expectedField);
    }

    // ---------------------------------------------------------------- happy path

    [Fact]
    public void ReferencePayload_IsValid()
    {
        var result = Validate(Payload());

        Assert.True(result.IsValid);
        Assert.Empty(result.Warnings);
        Assert.Equal(888.50m, result.DenominationTotal);

        var fields = Assert.IsType<DkBagRegistrationFields>(result.Fields);
        Assert.Equal("5790000123456", fields.CustomerRefNumber);
        Assert.Equal("POS-01", fields.CashRegisterId);
        Assert.Equal("0f8fad5b-d9cb-469f-a165-70867728950e", fields.MessageId);
        Assert.Equal("3164696800", fields.BagNumber);
        Assert.Equal(new DateOnly(2026, 8, 17), fields.RegistrationDate);
        Assert.Equal(888.50m, fields.TotalOrderAmount);
        Assert.Equal(10, fields.BagItemsLC!.Count);
    }

    [Fact]
    public void SingleDenomination_IsValid()
    {
        var result = Validate(Payload(totalOrderAmount: "\"500\"", bagItemsLC: """{ "DKK_500": "1" }"""));

        Assert.True(result.IsValid);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void ZeroQuantities_AreAllowed_AlongsideAPositiveOne()
    {
        var result = Validate(Payload(
            totalOrderAmount: "\"200\"",
            bagItemsLC: """{ "DKK_500": "0", "DKK_200": "1", "DKK_05": "0" }"""));

        Assert.True(result.IsValid);
    }

    // ---------------------------------------------------------------- structure

    [Fact]
    public void MalformedJson_IsRejected()
    {
        AssertFailsOn(Validate("{ not json"), "body");
    }

    [Theory]
    [InlineData("\"a string\"")]
    [InlineData("[1,2,3]")]
    [InlineData("42")]
    public void NonObjectBody_IsRejected(string body)
    {
        AssertFailsOn(Validate(body), "body");
    }

    [Fact]
    public void MissingBagRegistration_IsRejected()
    {
        AssertFailsOn(Validate("""{ "somethingElse": {} }"""), "bagRegistration");
    }

    [Fact]
    public void UnknownRootField_IsRejected()
    {
        AssertFailsOn(Validate(Payload(extraRootFields: ""","header": { "source": "POS" }""")), "header");
    }

    [Fact]
    public void UnknownDetailField_IsRejected()
    {
        AssertFailsOn(Validate(Payload(extraDetailFields: ""","currencyType": "DKK" """)), "bagRegistration.currencyType");
    }

    [Fact]
    public void BagRegistrationNotAnObject_IsRejected()
    {
        AssertFailsOn(Validate("""{ "bagRegistration": "nope" }"""), "bagRegistration");
    }

    // ---------------------------------------------------------------- mandatory fields

    [Theory]
    [InlineData("customerRefNumber")]
    [InlineData("cashRegisterId")]
    [InlineData("messageId")]
    [InlineData("bagNumber")]
    [InlineData("registrationDate")]
    [InlineData("totalOrderAmount")]
    public void MissingMandatoryField_IsRejected(string field)
    {
        // Rebuild the reference payload without the one field under test.
        var full = System.Text.Json.JsonDocument.Parse(Payload());
        var detail = full.RootElement.GetProperty("bagRegistration");

        var kept = detail.EnumerateObject()
            .Where(p => p.Name != field)
            .Select(p => $"\"{p.Name}\": {p.Value.GetRawText()}");

        var body = $$"""{ "bagRegistration": { {{string.Join(",", kept)}} } }""";

        AssertFailsOn(Validate(body), $"bagRegistration.{field}");
    }

    [Theory]
    [InlineData("customerRefNumber")]
    [InlineData("cashRegisterId")]
    public void EmptyStringField_IsRejected(string field)
    {
        var body = field == "customerRefNumber"
            ? Payload(customerRefNumber: "\"\"")
            : Payload(cashRegisterId: "\"   \"");

        AssertFailsOn(Validate(body), $"bagRegistration.{field}");
    }

    // Every value in this contract is a JSON string, including the numeric ones.
    [Fact]
    public void NumericFieldSentAsJsonNumber_IsRejected()
    {
        var result = Validate(Payload(totalOrderAmount: "888.50"));

        AssertFailsOn(result, "bagRegistration.totalOrderAmount");
        Assert.Contains("JSON string", result.JoinedMessages);
    }

    [Fact]
    public void NullField_IsRejected()
    {
        AssertFailsOn(Validate(Payload(bagNumber: "null")), "bagRegistration.bagNumber");
    }

    // ---------------------------------------------------------------- messageId

    [Theory]
    [InlineData("\"not-a-guid\"")]
    [InlineData("\"12345\"")]
    [InlineData("\"\"")]
    public void MessageIdThatIsNotAGuid_IsRejected(string messageId)
    {
        AssertFailsOn(Validate(Payload(messageId: messageId)), "bagRegistration.messageId");
    }

    // ---------------------------------------------------------------- bagNumber

    [Theory]
    [InlineData("\"316469680A\"")]
    [InlineData("\"3164-696800\"")]
    [InlineData("\" 3164696800\"")]
    public void NonNumericBagNumber_IsRejected(string bagNumber)
    {
        AssertFailsOn(Validate(Payload(bagNumber: bagNumber)), "bagRegistration.bagNumber");
    }

    [Fact]
    public void OverlongBagNumber_IsRejected()
    {
        AssertFailsOn(Validate(Payload(bagNumber: $"\"{new string('9', 21)}\"")), "bagRegistration.bagNumber");
    }

    // ---------------------------------------------------------------- registrationDate

    [Theory]
    [InlineData("\"17-08-2026\"")]
    [InlineData("\"2026/08/17\"")]
    [InlineData("\"2026-13-01\"")]
    [InlineData("\"2026-02-30\"")]
    [InlineData("\"2026-8-7\"")]
    [InlineData("\"2026-08-17T00:00:00Z\"")]
    public void BadRegistrationDate_IsRejected(string date)
    {
        AssertFailsOn(Validate(Payload(registrationDate: date)), "bagRegistration.registrationDate");
    }

    // ---------------------------------------------------------------- totalOrderAmount

    [Fact]
    public void CommaDecimalSeparator_IsRejectedWithAPointedMessage()
    {
        var result = Validate(Payload(totalOrderAmount: "\"888,50\""));

        AssertFailsOn(result, "bagRegistration.totalOrderAmount");
        Assert.Contains("decimal separator", result.JoinedMessages);
    }

    [Theory]
    [InlineData("\"0\"")]
    [InlineData("\"-5.00\"")]
    [InlineData("\"abc\"")]
    [InlineData("\"1 000\"")]
    public void InvalidTotalOrderAmount_IsRejected(string amount)
    {
        AssertFailsOn(Validate(Payload(totalOrderAmount: amount)), "bagRegistration.totalOrderAmount");
    }

    [Fact]
    public void MoreThanTwoDecimalPlaces_IsRejected()
    {
        AssertFailsOn(Validate(Payload(totalOrderAmount: "\"888.505\"")), "bagRegistration.totalOrderAmount");
    }

    // ---------------------------------------------------------------- bagItemsLC

    [Fact]
    public void UnknownDenomination_IsRejected()
    {
        var result = Validate(Payload(bagItemsLC: """{ "DKK_1000": "1" }"""));

        AssertFailsOn(result, "bagRegistration.bagItemsLC.DKK_1000");
        Assert.Contains("Allowed denominations", result.JoinedMessages);
    }

    [Fact]
    public void DenominationKeysAreCaseSensitive()
    {
        AssertFailsOn(Validate(Payload(bagItemsLC: """{ "dkk_500": "1" }""")), "bagRegistration.bagItemsLC.dkk_500");
    }

    [Fact]
    public void EmptyBagItems_IsAccepted()
    {
        // Carries no more information than omitting the optional field entirely.
        var result = Validate(Payload(bagItemsLC: "{}"));

        Assert.True(result.IsValid);
        Assert.Null(result.DenominationTotal);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void AllZeroQuantities_IsAccepted()
    {
        var result = Validate(Payload(bagItemsLC: """{ "DKK_500": "0", "DKK_100": "0" }"""));

        Assert.True(result.IsValid);
        Assert.Equal(0m, result.DenominationTotal);
    }

    [Fact]
    public void BagItemsLC_IsOptional_WhenOmittedEntirely()
    {
        var full = System.Text.Json.JsonDocument.Parse(Payload());
        var detail = full.RootElement.GetProperty("bagRegistration");
        var kept = detail.EnumerateObject()
            .Where(p => p.Name != "bagItemsLC")
            .Select(p => $"\"{p.Name}\": {p.Value.GetRawText()}");
        var body = $$"""{ "bagRegistration": { {{string.Join(",", kept)}} } }""";

        var result = Validate(body);

        Assert.True(result.IsValid);
        Assert.Null(result.Fields!.BagItemsLC);
        Assert.Null(result.DenominationTotal);
        // With no breakdown there is nothing to cross-check the declared total against.
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void BagItemsLC_IsOptional_WhenExplicitlyNull()
    {
        var result = Validate(Payload(bagItemsLC: "null"));

        Assert.True(result.IsValid);
        Assert.Null(result.Fields!.BagItemsLC);
    }

    [Fact]
    public void BagItemsNotAnObject_IsRejected()
    {
        AssertFailsOn(Validate(Payload(bagItemsLC: """[ "DKK_500" ]""")), "bagRegistration.bagItemsLC");
    }

    [Theory]
    [InlineData("\"-1\"")]
    [InlineData("\"1.5\"")]
    [InlineData("\"two\"")]
    [InlineData("\"\"")]
    public void InvalidQuantity_IsRejected(string quantity)
    {
        AssertFailsOn(Validate(Payload(bagItemsLC: $$"""{ "DKK_500": {{quantity}} }""")), "bagRegistration.bagItemsLC.DKK_500");
    }

    [Fact]
    public void QuantitySentAsJsonNumber_IsRejected()
    {
        AssertFailsOn(Validate(Payload(bagItemsLC: """{ "DKK_500": 1 }""")), "bagRegistration.bagItemsLC.DKK_500");
    }

    // ---------------------------------------------------------------- denomination total

    [Fact]
    public void TotalMismatch_IsAWarning_WhenNotEnforced()
    {
        // Declares 900.00 but the breakdown is one 500 note.
        var result = Validate(Payload(totalOrderAmount: "\"900.00\"", bagItemsLC: """{ "DKK_500": "1" }"""));

        Assert.True(result.IsValid);
        Assert.Single(result.Warnings);
        Assert.Contains("quantity of that denomination, not an amount", result.Warnings[0]);
        Assert.Equal(500m, result.DenominationTotal);
    }

    [Fact]
    public void TotalMismatch_IsAnError_WhenEnforced()
    {
        var result = Validate(
            Payload(totalOrderAmount: "\"900.00\"", bagItemsLC: """{ "DKK_500": "1" }"""),
            enforceTotal: true);

        AssertFailsOn(result, "bagRegistration.totalOrderAmount");
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void QuantityMistakenlySentAsAnAmount_ProducesTheMismatchWarning()
    {
        // The confusion the contract warns about: "500" meaning 500 kroner, not 500 notes.
        var result = Validate(Payload(totalOrderAmount: "\"500\"", bagItemsLC: """{ "DKK_500": "500" }"""));

        Assert.Single(result.Warnings);
        Assert.Equal(250000m, result.DenominationTotal);
    }

    // ---------------------------------------------------------------- error reporting

    [Fact]
    public void AllFailuresAreReported_NotJustTheFirst()
    {
        var result = Validate(Payload(
            customerRefNumber: "\"\"",
            messageId: "\"nope\"",
            bagNumber: "\"abc\"",
            registrationDate: "\"17-08-2026\""));

        Assert.False(result.IsValid);
        Assert.Equal(4, result.Failures.Count);
        Assert.Contains("bagRegistration.customerRefNumber", result.JoinedMessages);
        Assert.Contains("bagRegistration.messageId", result.JoinedMessages);
        Assert.Contains("bagRegistration.bagNumber", result.JoinedMessages);
        Assert.Contains("bagRegistration.registrationDate", result.JoinedMessages);
    }

    [Fact]
    public void SuppliedBagItems_AreStillValidated()
    {
        // Optional does not mean unchecked: a breakdown that IS sent must be well formed.
        AssertFailsOn(Validate(Payload(bagItemsLC: """{ "DKK_1000": "1" }""")), "bagRegistration.bagItemsLC.DKK_1000");
    }

    [Fact]
    public void FieldsAreNull_WhenValidationFails()
    {
        var result = Validate(Payload(bagNumber: "\"abc\""));

        Assert.False(result.IsValid);
        Assert.Null(result.Fields);
    }
}
