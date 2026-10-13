using System.Security.Claims;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Nokas.CashMgmt.No.Api.Controllers;
using Nokas.CashMgmt.No.Api.Documents;
using Nokas.CashMgmt.No.Api.Models;
using Nokas.CashMgmt.No.Api.Services;
using Nokas.CashMgmt.No.Shared.Contracts;
using Nokas.CashMgmt.Common.Correlation;
using Nokas.CashMgmt.Common.Errors;
using Xunit;

namespace Nokas.CashMgmt.No.Api.Tests.Controllers;

public class BagRegistrationControllerTests
{
    private const string ClientId = "1a732120-1149-4d97-98b5-d454d144b383";
    private const string BagNumber = "20250822162";

    private static BagRegistrationController CreateController(
        FakeBagRegistrationRepository repository, FakeBagStatusFunctionClient statusClient, string? clientId = ClientId)
    {
        var controller = new BagRegistrationController(
            repository,
            new FakeBagNumberCacheRepository(),
            statusClient,
            new TelemetryClient(new TelemetryConfiguration()),
            new InMemoryPayloadArchiveQueue(),
            NullLogger<BagRegistrationController>.Instance);

        var httpContext = new DefaultHttpContext();
        httpContext.Items[CorrelationConstants.HttpContextItemsKey] = "corr-1";
        if (clientId is not null)
        {
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("azp", clientId)]));
        }

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    private static BagRegistrationDocument CreateDocument(string status = "In-Progress") => new()
    {
        Id = BagNumber,
        ClientId = ClientId,
        BagLifecycleId = "lifecycle-1",
        CorrelationId = "corr-0",
        RegistrationStatus = status,
        BagRegistration = new BagRegistrationDetail
        {
            CustomerRefNumber = "5790002334764",
            RegistrationType = "NOK",
            MessageId = "msg-1",
            BagNumber = BagNumber,
            RegisteredBy = $"API_{ClientId}",
            TotalOrderAmount = "10000"
        }
    };

    [Fact]
    public async Task Get_MissingClientId_ReturnsUnauthorized()
    {
        var controller = CreateController(new FakeBagRegistrationRepository(), new FakeBagStatusFunctionClient(), clientId: null);

        var result = await controller.GetBagRegistration(BagNumber, CancellationToken.None);

        var objectResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("ERR_MISSING_CLIENT_ID", Assert.IsType<ErrorResponse>(objectResult.Value).Code);
    }

    [Fact]
    public async Task Get_BagNotFound_Returns404()
    {
        var controller = CreateController(new FakeBagRegistrationRepository(), new FakeBagStatusFunctionClient());

        var result = await controller.GetBagRegistration(BagNumber, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
    }

    [Fact]
    public async Task Get_BagFound_ReturnsOkWithCurrentStatus()
    {
        var repository = new FakeBagRegistrationRepository();
        repository.Documents[BagNumber] = CreateDocument();
        var controller = CreateController(repository, new FakeBagStatusFunctionClient());

        var result = await controller.GetBagRegistration(BagNumber, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<BagRegisteredResponse>(okResult.Value);
        Assert.Equal("In-Progress", body.RegistrationStatus);
    }

    [Fact]
    public async Task Delete_MissingClientId_ReturnsUnauthorized()
    {
        var controller = CreateController(new FakeBagRegistrationRepository(), new FakeBagStatusFunctionClient(), clientId: null);

        var result = await controller.DeleteBagRegistration(BagNumber, CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Delete_BagNotFound_Returns404()
    {
        var controller = CreateController(new FakeBagRegistrationRepository(), new FakeBagStatusFunctionClient());

        var result = await controller.DeleteBagRegistration(BagNumber, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
    }

    [Fact]
    public async Task Delete_StatusFunctionThrowsOperationCanceled_Returns408()
    {
        var repository = new FakeBagRegistrationRepository();
        repository.Documents[BagNumber] = CreateDocument();
        var statusClient = new FakeBagStatusFunctionClient { ExceptionToThrow = new OperationCanceledException() };
        var controller = CreateController(repository, statusClient);

        var result = await controller.DeleteBagRegistration(BagNumber, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status408RequestTimeout, objectResult.StatusCode);
    }

    [Fact]
    public async Task Delete_StatusFunctionThrowsGenericException_Returns503()
    {
        var repository = new FakeBagRegistrationRepository();
        repository.Documents[BagNumber] = CreateDocument();
        var statusClient = new FakeBagStatusFunctionClient { ExceptionToThrow = new InvalidOperationException("boom") };
        var controller = CreateController(repository, statusClient);

        var result = await controller.DeleteBagRegistration(BagNumber, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, objectResult.StatusCode);
    }

    [Fact]
    public async Task Delete_StatusFunctionReturnsNull_Returns404()
    {
        var repository = new FakeBagRegistrationRepository();
        repository.Documents[BagNumber] = CreateDocument();
        var statusClient = new FakeBagStatusFunctionClient { Result = null };
        var controller = CreateController(repository, statusClient);

        var result = await controller.DeleteBagRegistration(BagNumber, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
        Assert.Equal("ERR_BAG_STATUS_NOT_FOUND", Assert.IsType<ErrorResponse>(objectResult.Value).Code);
    }

    [Fact]
    public async Task Delete_AlreadyCancelled_Returns409AndSelfHealsStatus()
    {
        var repository = new FakeBagRegistrationRepository();
        repository.Documents[BagNumber] = CreateDocument(status: "In-Progress");
        var statusClient = new FakeBagStatusFunctionClient { Result = new BagStatusRecord { intStatus = 10 } };
        var controller = CreateController(repository, statusClient);

        var result = await controller.DeleteBagRegistration(BagNumber, CancellationToken.None);

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, objectResult.StatusCode);
        Assert.Equal("ERR_ALREADY_CANCELLED", Assert.IsType<ErrorResponse>(objectResult.Value).Code);
        Assert.Equal("Deleted", repository.Documents[BagNumber].RegistrationStatus);
        Assert.Empty(repository.WrittenOutboxMessages);
    }

    [Fact]
    public async Task Delete_NotCancellable_Returns400()
    {
        var repository = new FakeBagRegistrationRepository();
        repository.Documents[BagNumber] = CreateDocument();
        var statusClient = new FakeBagStatusFunctionClient { Result = new BagStatusRecord { intStatus = 5 } };
        var controller = CreateController(repository, statusClient);

        var result = await controller.DeleteBagRegistration(BagNumber, CancellationToken.None);

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
        Assert.Equal("ERR_NOT_CANCELLABLE", Assert.IsType<ErrorResponse>(objectResult.Value).Code);
        Assert.Empty(repository.WrittenOutboxMessages);
    }

    [Fact]
    public async Task Delete_Cancellable_Returns200AndWritesOutboxMessage()
    {
        var repository = new FakeBagRegistrationRepository();
        repository.Documents[BagNumber] = CreateDocument();
        var statusClient = new FakeBagStatusFunctionClient { Result = new BagStatusRecord { intStatus = 0, intInnkastnr = BagNumber } };
        var controller = CreateController(repository, statusClient);

        var result = await controller.DeleteBagRegistration(BagNumber, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<BagRegisteredResponse>(okResult.Value);
        Assert.Equal("Deleted", body.RegistrationStatus);
        Assert.Equal("Deleted", repository.Documents[BagNumber].RegistrationStatus);
        Assert.Equal("Cancelled", repository.Documents[BagNumber].BagRegistration.ActionFlag);

        var outbox = Assert.Single(repository.WrittenOutboxMessages);
        Assert.Equal("deleteBagRegistration", outbox.Subject);
    }
}
