using System.Security.Claims;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nokas.CashMgmt.No.Api.Configuration;
using Nokas.CashMgmt.No.Api.Controllers;
using Nokas.CashMgmt.No.Api.Models;
using Nokas.CashMgmt.No.Api.Services;
using Nokas.CashMgmt.Common.Correlation;
using Nokas.CashMgmt.No.Shared.Documents;
using Nokas.CashMgmt.Common.Errors;
using Xunit;

namespace Nokas.CashMgmt.No.Api.Tests.Controllers;

public class BagCountingResultsControllerTests
{
    private const string ClientId = "1a732120-1149-4d97-98b5-d454d144b383";
    private const string BagNumber = "20250822162";

    private static BagCountingResultsController CreateController(
        FakeCountingResultRepository repository, FakeContinuationTokenService? tokenService = null, string? clientId = ClientId)
    {
        var controller = new BagCountingResultsController(
            repository,
            tokenService ?? new FakeContinuationTokenService(),
            Options.Create(new CountingResultsOptions
            {
                Query = new CountingResultsOptions.QuerySettings { MaxSpanDays = 92, MaxPageSize = 500, DefaultPageSize = 100 }
            }),
            new TelemetryClient(new TelemetryConfiguration()),
            NullLogger<BagCountingResultsController>.Instance);

        var httpContext = new DefaultHttpContext();
        httpContext.Items[CorrelationConstants.HttpContextItemsKey] = "corr-1";
        if (clientId is not null)
        {
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("azp", clientId)]));
        }

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    private static CountingResultDocument CreateDocument() => new()
    {
        Id = "12345",
        ClientId = ClientId,
        NattsafeRecordId = 12345,
        BagNumber = BagNumber,
        CustomerRefNumber = "5790002334764",
        CustomerNumber = "1001",
        Status = "counted",
        StatusCode = 5,
        DeclaredAmount = 10000m,
        CountedAmount = 9900m,
        DeviationAmount = -100m,
        RegisteredAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        CountedAt = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
        LastUpdatedAt = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc)
    };

    [Fact]
    public async Task Query_MissingClientId_ReturnsUnauthorized()
    {
        var controller = CreateController(new FakeCountingResultRepository(), clientId: null);

        var result = await controller.QueryCountingResults(new CountingResultsQueryRequest
        {
            FromDate = DateTime.UtcNow.AddDays(-1),
            ToDate = DateTime.UtcNow
        }, CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Query_MissingDates_ReturnsValidationError()
    {
        var controller = CreateController(new FakeCountingResultRepository());

        var result = await controller.QueryCountingResults(new CountingResultsQueryRequest(), CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Equal("VALIDATION_ERROR", error.Code);
        Assert.Equal("fromDate", error.Field);
    }

    [Fact]
    public async Task Query_ToDateBeforeFromDate_ReturnsValidationError()
    {
        var controller = CreateController(new FakeCountingResultRepository());

        var result = await controller.QueryCountingResults(new CountingResultsQueryRequest
        {
            FromDate = DateTime.UtcNow,
            ToDate = DateTime.UtcNow.AddDays(-1)
        }, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Equal("toDate", error.Field);
    }

    [Fact]
    public async Task Query_SpanExceedsMaxDays_ReturnsValidationError()
    {
        var controller = CreateController(new FakeCountingResultRepository());

        var result = await controller.QueryCountingResults(new CountingResultsQueryRequest
        {
            FromDate = DateTime.UtcNow.AddDays(-200),
            ToDate = DateTime.UtcNow
        }, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Equal("toDate", error.Field);
    }

    [Fact]
    public async Task Query_InvalidDateType_ReturnsValidationError()
    {
        var controller = CreateController(new FakeCountingResultRepository());

        var result = await controller.QueryCountingResults(new CountingResultsQueryRequest
        {
            FromDate = DateTime.UtcNow.AddDays(-1),
            ToDate = DateTime.UtcNow,
            DateType = "notarealtype"
        }, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Equal("dateType", error.Field);
    }

    [Fact]
    public async Task Query_InvalidStatusBucket_ReturnsValidationError()
    {
        var controller = CreateController(new FakeCountingResultRepository());

        var result = await controller.QueryCountingResults(new CountingResultsQueryRequest
        {
            FromDate = DateTime.UtcNow.AddDays(-1),
            ToDate = DateTime.UtcNow,
            Status = "received by bank" // granular label, not a valid bucket
        }, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Equal("status", error.Field);
    }

    [Fact]
    public async Task Query_PageSizeOutOfRange_ReturnsValidationError()
    {
        var controller = CreateController(new FakeCountingResultRepository());

        var result = await controller.QueryCountingResults(new CountingResultsQueryRequest
        {
            FromDate = DateTime.UtcNow.AddDays(-1),
            ToDate = DateTime.UtcNow,
            PageSize = 5000
        }, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Equal("pageSize", error.Field);
    }

    [Fact]
    public async Task Query_ContinuationTokenHashMismatch_ReturnsInvalidContinuationToken()
    {
        var tokenService = new FakeContinuationTokenService
        {
            UnwrapResult = new ContinuationTokenPayload("native-token", "some-other-hash")
        };
        var controller = CreateController(new FakeCountingResultRepository(), tokenService);

        var result = await controller.QueryCountingResults(new CountingResultsQueryRequest
        {
            FromDate = DateTime.UtcNow.AddDays(-1),
            ToDate = DateTime.UtcNow,
            ContinuationToken = "opaque-token"
        }, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
        var error = Assert.IsType<ErrorResponse>(objectResult.Value);
        Assert.Equal("INVALID_CONTINUATION_TOKEN", error.Code);
    }

    [Fact]
    public async Task Query_ContinuationTokenUnwrapFails_ReturnsInvalidContinuationToken()
    {
        var tokenService = new FakeContinuationTokenService { UnwrapResult = null };
        var controller = CreateController(new FakeCountingResultRepository(), tokenService);

        var result = await controller.QueryCountingResults(new CountingResultsQueryRequest
        {
            FromDate = DateTime.UtcNow.AddDays(-1),
            ToDate = DateTime.UtcNow,
            ContinuationToken = "garbage"
        }, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
        var error = Assert.IsType<ErrorResponse>(objectResult.Value);
        Assert.Equal("INVALID_CONTINUATION_TOKEN", error.Code);
    }

    [Fact]
    public async Task Query_ValidRequest_ReturnsOkWithMappedItemsAndPagination()
    {
        var document = CreateDocument();
        var repository = new FakeCountingResultRepository
        {
            QueryResult = new Nokas.CashMgmt.No.Api.Repositories.CountingResultPage([document], "native-next-token"),
            DataAsOfResult = new DateTime(2026, 1, 2, 0, 30, 0, DateTimeKind.Utc)
        };
        var controller = CreateController(repository);

        var result = await controller.QueryCountingResults(new CountingResultsQueryRequest
        {
            FromDate = DateTime.UtcNow.AddDays(-1),
            ToDate = DateTime.UtcNow
        }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<CountingResultsResponseDto>(ok.Value);
        Assert.Single(response.Items);
        Assert.Equal(BagNumber, response.Items[0].BagNumber);
        Assert.Equal("counted", response.Items[0].Status);
        Assert.True(response.Pagination.HasMore);
        Assert.NotNull(response.Pagination.ContinuationToken);
        Assert.Equal(repository.DataAsOfResult, response.DataAsOf);
    }

    [Fact]
    public async Task GetByBagNumber_MissingClientId_ReturnsUnauthorized()
    {
        var controller = CreateController(new FakeCountingResultRepository(), clientId: null);

        var result = await controller.GetCountingResultByBagNumber(BagNumber, CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task GetByBagNumber_NotFound_ReturnsNotFound()
    {
        var controller = CreateController(new FakeCountingResultRepository { SingleResult = null });

        var result = await controller.GetCountingResultByBagNumber(BagNumber, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
        var error = Assert.IsType<ErrorResponse>(objectResult.Value);
        Assert.Equal("NOT_FOUND", error.Code);
    }

    [Fact]
    public async Task GetByBagNumber_Found_ReturnsOkWithMappedItem()
    {
        var controller = CreateController(new FakeCountingResultRepository { SingleResult = CreateDocument() });

        var result = await controller.GetCountingResultByBagNumber(BagNumber, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var item = Assert.IsType<CountingResultItemDto>(ok.Value);
        Assert.Equal(BagNumber, item.BagNumber);
        Assert.Equal("counted", item.Status);
        Assert.Equal(-100m, item.DeviationAmount);
    }
}
