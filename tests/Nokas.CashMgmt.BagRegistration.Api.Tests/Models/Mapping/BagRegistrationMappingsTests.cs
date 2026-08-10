using Nokas.CashMgmt.BagRegistration.Api.Mapping;
using Nokas.CashMgmt.BagRegistration.Api.Models;
using Xunit;

namespace Nokas.CashMgmt.BagRegistration.Api.Tests.Mapping;

public class BagRegistrationMappingsTests
{
    [Fact]
    public void ToDocument_SetsRegisteredByFromClientId()
    {
        var dto = new BagRegistrationDto
        {
            CustomerRefNumber = "5790002334764",
            RegistrationType = "NOK",
            MessageId = Guid.NewGuid().ToString(),
            BagNumber = "20250822162",
            TotalOrderAmount = "10000"
        };

        var document = dto.ToDocument(
            clientId: "1a732120-1149-4d97-98b5-d454d144b383",
            bagLifecycleId: "life-1",
            correlationId: "corr-1");

        Assert.Equal("API_1a732120-1149-4d97-98b5-d454d144b383", document.BagRegistration.RegisteredBy);
        Assert.Equal(dto.BagNumber, document.Id);
        Assert.Equal("In-Progress", document.RegistrationStatus);
        Assert.Single(document.OperationHistory);
    }
}