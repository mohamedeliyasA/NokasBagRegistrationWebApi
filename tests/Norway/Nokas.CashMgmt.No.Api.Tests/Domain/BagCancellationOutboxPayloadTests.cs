using System.Text.Json;
using System.Text.Json.Serialization;
using Nokas.CashMgmt.No.Api.Domain;
using Nokas.CashMgmt.No.Shared.Contracts;
using Xunit;

namespace Nokas.CashMgmt.No.Api.Tests.Domain;

// Golden-file coverage for the "deleteBagRegistration" Service Bus message. No XSD covers
// this subject, so the old (currently-live) controller's exact field set/order is the
// contract - including that null fields are omitted, not emitted as null.
public class BagCancellationOutboxPayloadTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [Fact]
    public void From_CancelledBag_ProducesExpectedWireShape()
    {
        var status = new BagStatusRecord
        {
            intInnkastnr = "20250822162",
            vchCustomer_id = "314211",
            vchName = "JEM AND FIX NORGE Avd Fredrikstad",
            dblAmount = "10000.00",
            vchLocation = "Oslo",
            dtmRegDate = new DateTime(2026, 8, 10, 22, 45, 0, DateTimeKind.Unspecified),
            dtmTransportFetch = null,
            intStatus = 1
        };

        var payload = BagCancellationOutboxPayload.From(
            customerRefNumber: "5790002334764",
            bagNumber: "20250822162",
            registeredBy: "API_1a732120-1149-4d97-98b5-d454d144b383",
            cancellationDateUtc: new DateTime(2026, 8, 10, 20, 50, 0, DateTimeKind.Utc),
            status: status);

        var json = JsonSerializer.Serialize(payload, SerializerOptions);

        const string expected = """
            {"cancelBagRegistration":{"customerRefNumber":"5790002334764","bagNumber":"20250822162","actionFlag":"cancel","registeredBy":"API_1a732120-1149-4d97-98b5-d454d144b383","cancellationDate":"2026-08-10","intInnkastnr":"20250822162","vchCustomer_id":"314211","vchName":"JEM AND FIX NORGE Avd Fredrikstad","dblAmount":"10000.00","vchLocation":"Oslo","dtmRegDate":"2026-08-10T22:45:00","intStatus":1}}
            """;

        Assert.Equal(expected.Trim(), json);
    }

    [Fact]
    public void From_NullStatusFields_AreOmittedNotEmittedAsNull()
    {
        var status = new BagStatusRecord();

        var payload = BagCancellationOutboxPayload.From(
            customerRefNumber: "ref", bagNumber: "123", registeredBy: "API_test",
            cancellationDateUtc: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), status: status);

        var json = JsonSerializer.Serialize(payload, SerializerOptions);

        Assert.DoesNotContain("intInnkastnr", json);
        Assert.DoesNotContain("vchCustomer_id", json);
        Assert.DoesNotContain("dtmRegDate", json);
        Assert.DoesNotContain("intStatus", json);
    }
}
