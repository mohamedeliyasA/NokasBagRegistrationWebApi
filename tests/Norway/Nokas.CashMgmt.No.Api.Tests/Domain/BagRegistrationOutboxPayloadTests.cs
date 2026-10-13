using System.Text.Json;
using Nokas.CashMgmt.No.Api.Domain;
using Nokas.CashMgmt.No.Api.Models;
using Xunit;

namespace Nokas.CashMgmt.No.Api.Tests.Domain;

// Golden-file coverage for the "bagregistration" Service Bus message. The exact field set
// and order are frozen by BizTalk/BagRegistration.xsd - actionFlag and registeredBy are
// mandatory there and must always be present, even though the partner never supplies them.
public class BagRegistrationOutboxPayloadTests
{
    [Fact]
    public void From_NokRegistration_ProducesExpectedWireShape()
    {
        var request = new BagRegistrationDto
        {
            CustomerRefNumber = "5790002334764",
            RegistrationType = "NOK",
            MessageId = "msg-1",
            BagNumber = "20250822162",
            RegistrationDate = "2026-08-10",
            TurnOverDate = null,
            RegisteredNotes = null,
            RegisteredCoins = null,
            RegisteredChecks = null,
            TotalOrderAmount = "10000",
            BagItemsLC = null,
            BagItemsFC = null
        };

        var payload = BagRegistrationOutboxPayload.From(request, registeredBy: "API_1a732120-1149-4d97-98b5-d454d144b383");
        var json = JsonSerializer.Serialize(payload);

        const string expected = """
            {"bagRegistration":{"customerRefNumber":"5790002334764","registrationType":"NOK","messageId":"msg-1","bagNumber":"20250822162","actionFlag":"Create","registeredBy":"API_1a732120-1149-4d97-98b5-d454d144b383","registrationDate":"2026-08-10","turnOverDate":null,"registeredNotes":null,"registeredCoins":null,"registeredChecks":null,"totalOrderAmount":"10000","bagItemsLC":null,"bagItemsFC":null}}
            """;

        Assert.Equal(expected.Trim(), json);
    }

    [Fact]
    public void From_AlwaysSetsActionFlagCreate()
    {
        var request = new BagRegistrationDto
        {
            CustomerRefNumber = "ref",
            RegistrationType = "NOK",
            MessageId = "msg",
            BagNumber = "123",
            TotalOrderAmount = "1"
        };

        var payload = BagRegistrationOutboxPayload.From(request, registeredBy: "API_test");

        Assert.Equal("Create", payload.BagRegistration.ActionFlag);
        Assert.Equal("API_test", payload.BagRegistration.RegisteredBy);
    }
}
