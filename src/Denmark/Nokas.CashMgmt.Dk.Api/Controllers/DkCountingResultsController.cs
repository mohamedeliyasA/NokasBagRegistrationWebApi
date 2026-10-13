using System.Diagnostics;
using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Nokas.CashMgmt.Dk.Api.Configuration;
using Nokas.CashMgmt.Dk.Api.Helpers;
using Nokas.CashMgmt.Dk.Api.Mapping;
using Nokas.CashMgmt.Dk.Api.Models;
using Nokas.CashMgmt.Dk.Api.Repositories;
using Nokas.CashMgmt.Dk.Api.Services;
using Nokas.CashMgmt.Common.Correlation;
using Nokas.CashMgmt.Common.Errors;
using Nokas.CashMgmt.Dk.Shared.Repositories;
using Nokas.CashMgmt.Dk.Shared.Services;

namespace Nokas.CashMgmt.Dk.Api.Controllers;

/// <summary>
/// Danish counting results, served from Cosmos - never from ServiceNow at request time (see
/// DkCountingResultSyncBackgroundService). Same contract shape as Norway's, except that
/// <c>status</c> is the small "registered | counted | unknown" vocabulary rather than Norway's
/// six buckets.
/// </summary>
[Authorize(Policy = AuthConfig.CountingResultRolePolicy)]
[Route("api/cash-mgmt/dk/bag-counting-results")]
[ApiController]
[Produces("application/json", "application/xml")]
public sealed class DkCountingResultsController(
    IDkCountingResultRepository repository,
    IContinuationTokenService tokenService,
    IOptions<DkCountingResultsOptions> options,
    TelemetryClient telemetryClient,
    ILogger<DkCountingResultsController> logger) : ControllerBase
{
    private static readonly string[] ValidDateTypes = ["lastUpdated", "registered", "counted"];

    /// <summary>Lists counting results for the caller's own bags, newest first.</summary>
    /// <remarks>
    /// fromDate and toDate are required. A toDate with no time covers the whole of that day.
    /// dateType chooses what they filter and sort on:
    /// lastUpdated (default), registered, or counted. Page through with
    /// the returned continuationToken, passing the SAME other parameters - a token replayed
    /// against a different query is rejected. Compare <c>dataAsOf</c> with the current time to
    /// detect a stale feed.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(DkCountingResultsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> QueryCountingResults([FromQuery] DkCountingResultsQueryRequest request, CancellationToken cancellationToken)
    {
        var correlationId = HttpContext.Items[CorrelationConstants.HttpContextItemsKey]?.ToString() ?? "N/A";
        var clientId = User.GetClientId();

        if (string.IsNullOrEmpty(clientId))
        {
            logger.LogWarning("CorrelationId: {CorrelationId} - GET DK counting-results rejected, token missing clientId claim.", correlationId);
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

        // A date with no time means the whole of that day. Otherwise "toDate=2026-09-23" silently
        // excludes everything registered that day, which reads as missing data rather than as a
        // range that ended at midnight. Pass an explicit time to get an exact cut-off.
        if (toDate.TimeOfDay == TimeSpan.Zero)
        {
            toDate = toDate.AddDays(1).AddTicks(-1);
        }

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

        if (request.Status is not null && !DkCountingResultStatusMapper.ValidBuckets.Contains(request.Status))
        {
            return ValidationError(correlationId, $"status must be one of: {string.Join(", ", DkCountingResultStatusMapper.ValidBuckets)}.", "status");
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
                logger.LogWarning("CorrelationId: {CorrelationId} - Rejected DK continuation token (missing/tampered/query mismatch).", correlationId);
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
            new DkCountingResultQueryOptions(fromDate, toDate, request.DateType, request.Status, request.CustomerRefNumber, pageSize),
            nativeToken,
            cancellationToken);

        var dataAsOf = await repository.GetDataAsOfAsync(cancellationToken);

        stopwatch.Stop();

        var responseToken = page.ContinuationToken is null ? null : tokenService.Wrap(page.ContinuationToken, expectedHash);

        telemetryClient.TrackEvent("DkCountingResultQuery", new Dictionary<string, string>
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

        return Ok(new DkCountingResultsResponseDto
        {
            Items = [.. page.Items.Select(d => d.ToDto())],
            Pagination = new DkPaginationDto
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

    /// <summary>Returns the most recently updated counting result for a bag number.</summary>
    [HttpGet("{bagNumber}")]
    [ProducesResponseType(typeof(DkCountingResultItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCountingResultByBagNumber(string bagNumber, CancellationToken cancellationToken)
    {
        var correlationId = HttpContext.Items[CorrelationConstants.HttpContextItemsKey]?.ToString() ?? "N/A";
        var clientId = User.GetClientId();

        if (string.IsNullOrEmpty(clientId))
        {
            logger.LogWarning("CorrelationId: {CorrelationId} - GET DK counting-result rejected, token missing clientId claim.", correlationId);
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
            logger.LogWarning("CorrelationId: {CorrelationId} - DK counting result not found for bag {BagNumber}.", correlationId, bagNumber);
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
        logger.LogWarning("CorrelationId: {CorrelationId} - DK counting-result query validation failed: {Message}", correlationId, message);
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
