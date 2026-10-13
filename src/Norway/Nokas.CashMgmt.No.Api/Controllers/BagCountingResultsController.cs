using System.Diagnostics;
using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Nokas.CashMgmt.No.Api.Configuration;
using Nokas.CashMgmt.No.Api.Helpers;
using Nokas.CashMgmt.No.Api.Mapping;
using Nokas.CashMgmt.No.Api.Models;
using Nokas.CashMgmt.No.Api.Repositories;
using Nokas.CashMgmt.No.Api.Services;
using Nokas.CashMgmt.Common.Correlation;
using Nokas.CashMgmt.Common.Errors;
using Nokas.CashMgmt.No.Shared.StatusMapping;

namespace Nokas.CashMgmt.No.Api.Controllers;

[Authorize(Policy = AuthConfig.CountingResultRolePolicy)]
[Route("api/cash-mgmt/bag-counting-results")]
[ApiController]
[Produces("application/json", "application/xml")]
public sealed class BagCountingResultsController(
    ICountingResultRepository repository,
    IContinuationTokenService tokenService,
    IOptions<CountingResultsOptions> options,
    TelemetryClient telemetryClient,
    ILogger<BagCountingResultsController> logger) : ControllerBase
{
    private static readonly string[] ValidDateTypes = ["counted", "registered", "lastUpdated"];

    [HttpGet]
    [ProducesResponseType(typeof(CountingResultsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> QueryCountingResults([FromQuery] CountingResultsQueryRequest request, CancellationToken cancellationToken)
    {
        var correlationId = HttpContext.Items[CorrelationConstants.HttpContextItemsKey]?.ToString() ?? "N/A";
        var clientId = User.GetClientId();

        if (string.IsNullOrEmpty(clientId))
        {
            logger.LogWarning("CorrelationId: {CorrelationId} - GET counting-results rejected, token missing clientId claim.", correlationId);
            return Unauthorized(new ErrorResponse
            {
                Title = "Unauthorized",
                Status = StatusCodes.Status401Unauthorized,
                CorrelationId = correlationId,
                Code = "ERR_MISSING_CLIENT_ID"
            });
        }

        var settings = options.Value.Query;

        if (request.FromDate is null || request.ToDate is null)
        {
            return ValidationError(correlationId, "fromDate and toDate are required.", "fromDate");
        }

        var fromDate = DateTime.SpecifyKind(request.FromDate.Value, DateTimeKind.Utc);
        var toDate = DateTime.SpecifyKind(request.ToDate.Value, DateTimeKind.Utc);

        if (toDate < fromDate)
        {
            return ValidationError(correlationId, "toDate must not be before fromDate.", "toDate");
        }

        if ((toDate - fromDate).TotalDays > settings.MaxSpanDays)
        {
            return ValidationError(correlationId, $"Date range must not exceed {settings.MaxSpanDays} days.", "toDate");
        }

        if (!ValidDateTypes.Contains(request.DateType))
        {
            return ValidationError(correlationId, $"dateType must be one of: {string.Join(", ", ValidDateTypes)}.", "dateType");
        }

        if (request.Status is not null && !NattsafeStatusMapper.ValidBuckets.Contains(request.Status))
        {
            return ValidationError(correlationId, $"status must be one of: {string.Join(", ", NattsafeStatusMapper.ValidBuckets)}.", "status");
        }

        var pageSize = request.PageSize ?? settings.DefaultPageSize;
        if (pageSize < 1 || pageSize > settings.MaxPageSize)
        {
            return ValidationError(correlationId, $"pageSize must be between 1 and {settings.MaxPageSize}.", "pageSize");
        }

        var expectedHash = tokenService.ComputeQueryHash(clientId, fromDate, toDate, request.DateType, request.Status, request.CustomerRefNumber, pageSize);

        string? nativeToken = null;
        if (!string.IsNullOrEmpty(request.ContinuationToken))
        {
            var unwrapped = tokenService.TryUnwrap(request.ContinuationToken);
            if (unwrapped is null || unwrapped.QueryHash != expectedHash)
            {
                logger.LogWarning("CorrelationId: {CorrelationId} - Rejected continuation token (missing/tampered/query mismatch).", correlationId);
                return StatusCode(StatusCodes.Status400BadRequest, new ErrorResponse
                {
                    Title = "Continuation token is invalid or does not match the supplied query parameters.",
                    Status = StatusCodes.Status400BadRequest,
                    CorrelationId = correlationId,
                    Code = "INVALID_CONTINUATION_TOKEN"
                });
            }

            nativeToken = unwrapped.NativeToken;
        }

        var stopwatch = Stopwatch.StartNew();

        var page = await repository.QueryAsync(
            clientId,
            new CountingResultQueryOptions(fromDate, toDate, request.DateType, request.Status, request.CustomerRefNumber, pageSize),
            nativeToken,
            cancellationToken);

        var dataAsOf = await repository.GetDataAsOfAsync(cancellationToken);

        stopwatch.Stop();

        var responseToken = page.ContinuationToken is null ? null : tokenService.Wrap(page.ContinuationToken, expectedHash);

        telemetryClient.TrackEvent("CountingResultQuery", new Dictionary<string, string>
        {
            ["clientId"] = clientId,
            ["dateType"] = request.DateType,
            ["fromDate"] = fromDate.ToString("O"),
            ["toDate"] = toDate.ToString("O"),
            ["statusFilter"] = request.Status ?? "",
            ["storeFilter"] = request.CustomerRefNumber ?? "",
            ["itemCount"] = page.Items.Count.ToString(),
            ["pageIndex"] = (nativeToken is null ? 0 : 1).ToString(),
            ["hasMore"] = (page.ContinuationToken is not null).ToString(),
            ["correlationId"] = correlationId
        }, new Dictionary<string, double>
        {
            ["durationMs"] = stopwatch.Elapsed.TotalMilliseconds
        });

        return Ok(new CountingResultsResponseDto
        {
            Items = [.. page.Items.Select(d => d.ToDto())],
            Pagination = new PaginationDto
            {
                PageSize = pageSize,
                Count = page.Items.Count,
                HasMore = page.ContinuationToken is not null,
                ContinuationToken = responseToken
            },
            DataAsOf = dataAsOf,
            CorrelationId = correlationId
        });
    }

    [HttpGet("{bagNumber}")]
    [ProducesResponseType(typeof(CountingResultItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCountingResultByBagNumber(string bagNumber, CancellationToken cancellationToken)
    {
        var correlationId = HttpContext.Items[CorrelationConstants.HttpContextItemsKey]?.ToString() ?? "N/A";
        var clientId = User.GetClientId();

        if (string.IsNullOrEmpty(clientId))
        {
            logger.LogWarning("CorrelationId: {CorrelationId} - GET counting-result rejected, token missing clientId claim.", correlationId);
            return Unauthorized(new ErrorResponse
            {
                Title = "Unauthorized",
                Status = StatusCodes.Status401Unauthorized,
                CorrelationId = correlationId,
                Code = "ERR_MISSING_CLIENT_ID"
            });
        }

        var document = await repository.GetLatestByBagNumberAsync(clientId, bagNumber, cancellationToken);
        if (document is null)
        {
            logger.LogWarning("CorrelationId: {CorrelationId} - Counting result not found for bag {BagNumber}.", correlationId, bagNumber);
            return StatusCode(StatusCodes.Status404NotFound, new ErrorResponse
            {
                Title = $"No counting result found for bag number '{bagNumber}' for this client.",
                Status = StatusCodes.Status404NotFound,
                CorrelationId = correlationId,
                Code = "NOT_FOUND"
            });
        }

        return Ok(document.ToDto());
    }

    private BadRequestObjectResult ValidationError(string correlationId, string message, string field)
    {
        logger.LogWarning("CorrelationId: {CorrelationId} - Counting-result query validation failed: {Message}", correlationId, message);
        return BadRequest(new ErrorResponse
        {
            Title = message,
            Status = StatusCodes.Status400BadRequest,
            CorrelationId = correlationId,
            Code = "VALIDATION_ERROR",
            Field = field
        });
    }
}
