using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nokas.CashMgmt.BagRegistration.Api.Configuration;
using Nokas.CashMgmt.BagRegistration.Api.Documents;
using Nokas.CashMgmt.BagRegistration.Api.Domain;
using Nokas.CashMgmt.BagRegistration.Api.Helpers;
using Nokas.CashMgmt.BagRegistration.Api.Mapping;
using Nokas.CashMgmt.BagRegistration.Api.Models;
using Nokas.CashMgmt.BagRegistration.Api.Repositories;
using Nokas.CashMgmt.BagRegistration.Api.Services;
using Nokas.CashMgmt.Shared.Contracts;
using Nokas.CashMgmt.Shared.Correlation;
using Nokas.CashMgmt.Shared.Errors;
using Nokas.CashMgmt.Shared.Results;

namespace Nokas.CashMgmt.BagRegistration.Api.Controllers;

[Authorize(Policy = AuthConfig.RegistrationRolePolicy)]
[Route("api/cash-mgmt/bag-registration")]
[ApiController]
[Produces("application/json", "application/xml")]
public sealed class BagRegistrationController(
    IBagRegistrationRepository repository,
    IBagNumberCacheRepository bagNumberCache,
    IBagStatusFunctionClient bagStatusFunctionClient,
    TelemetryClient telemetryClient,
    IPayloadArchiveQueue archiveQueue,
    ILogger<BagRegistrationController> logger) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(BagRegisteredResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CreateBagRegistration(
        [FromBody] BagRegistrationRequestDto request,
        CancellationToken cancellationToken)
    {
        var correlationId = HttpContext.Items[CorrelationConstants.HttpContextItemsKey]?.ToString() ?? "N/A";
        var clientId = User.GetClientId();

        if (string.IsNullOrEmpty(clientId))
        {
            logger.LogWarning("CorrelationId: {CorrelationId} - POST rejected, token missing clientId claim.", correlationId);
            return Unauthorized(new ErrorResponse
            {
                Title = "Unauthorized",
                Status = StatusCodes.Status401Unauthorized,
                CorrelationId = correlationId,
                Code = "ERR_MISSING_CLIENT_ID"
            });
        }

        var bagNumber = request.BagRegistration.BagNumber;

        bool exists;
        DateTime? cacheLastSyncedAt;
        try
        {
            exists = await bagNumberCache.ExistsAsync(bagNumber, cancellationToken);
            cacheLastSyncedAt = await bagNumberCache.GetLastSyncedAtAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "CorrelationId: {CorrelationId} - Unable to read bag-number cache for bag {BagNumber}",
                correlationId, bagNumber);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ErrorResponse
            {
                Title = "Unable to validate bag number.",
                Status = StatusCodes.Status503ServiceUnavailable,
                CorrelationId = correlationId,
                Code = "ERR_VALIDATE_UNAVAILABLE"
            });
        }

        if (exists)
        {
            logger.LogWarning("CorrelationId: {CorrelationId} - Bag already exists in system: {BagNumber}", correlationId, bagNumber);
            return Conflict(new ErrorResponse
            {
                Title = $"Bag {bagNumber} already exists and cannot be re-registered.",
                Status = StatusCodes.Status409Conflict,
                CorrelationId = correlationId,
                Code = "ERR_DUPLICATE_BAG",
                Field = "BagNumber"
            });
        }

        var bagLifecycleId = Guid.NewGuid().ToString();
        var document = request.BagRegistration.ToDocument(clientId, bagLifecycleId, correlationId);

        if (HttpContext.Items["ArchivedBlobName"] is string blobName)
        {
            document.OperationHistory[0] = document.OperationHistory[0] with { ArchiveBlobName = blobName };
            logger.LogInformation("Payload queued for archiving. BlobName: {BlobName}", blobName);
        }

        var outboxMessage = new OutboxMessageDocument
        {
            ClientId = clientId,
            Subject = "bagregistration",
            Payload = JsonSerializer.Serialize(BagRegistrationOutboxPayload.From(request.BagRegistration, document.BagRegistration.RegisteredBy))
        };

        var createResult = await repository.CreateAsync(document, outboxMessage, cancellationToken);

        if (!createResult.IsSuccess)
        {
            var error = createResult.Error!;
            var statusCode = error.Kind switch
            {
                ErrorKind.Conflict => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status500InternalServerError
            };

            if (error.Kind == ErrorKind.Conflict)
            {
                logger.LogWarning("CorrelationId: {CorrelationId} - Duplicate bag number error: {Message}", correlationId, error.Message);
            }

            return StatusCode(statusCode, ToErrorResponse(error, correlationId));
        }

        logger.LogInformation("BagLifecycleId: {BagLifecycleId}, CorrelationId: {CorrelationId} - Bag registration created successfully.",
            bagLifecycleId, correlationId);

        var responseBody = new BagRegisteredResponse
        {
            CustomerRefNumber = document.BagRegistration.CustomerRefNumber,
            BagNumber = document.BagRegistration.BagNumber,
            RegistrationStatus = document.RegistrationStatus,
            Description = "Bag Registered Successfully",
            BagLifecycleId = bagLifecycleId,
            RequestCorrelationId = correlationId
        };

        telemetryClient.TrackEvent("BagNumberCacheCheck", new Dictionary<string, string>
        {
            ["bagNumber"] = bagNumber,
            ["bagLifecycleId"] = bagLifecycleId,
            ["clientId"] = clientId,
            ["correlationId"] = correlationId,
            ["cacheLastSyncedAt"] = cacheLastSyncedAt?.ToString("O") ?? "unknown"
        });

        return StatusCode(StatusCodes.Status201Created, responseBody);
    }

    [HttpGet("{bagNumber}")]
    [ProducesResponseType(typeof(BagRegisteredResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBagRegistration(string bagNumber, CancellationToken cancellationToken)
    {
        var correlationId = HttpContext.Items[CorrelationConstants.HttpContextItemsKey]?.ToString() ?? "N/A";
        var clientId = User.GetClientId();

        if (string.IsNullOrEmpty(clientId))
        {
            logger.LogWarning("CorrelationId: {CorrelationId} - GET rejected, token missing clientId claim.", correlationId);
            return Unauthorized(new ErrorResponse
            {
                Title = "Unauthorized",
                Status = StatusCodes.Status401Unauthorized,
                CorrelationId = correlationId,
                Code = "ERR_MISSING_CLIENT_ID"
            });
        }

        var getResult = await repository.GetByBagNumberForClientAsync(bagNumber, clientId, cancellationToken);
        if (!getResult.IsSuccess)
        {
            var error = getResult.Error!;
            logger.LogWarning("CorrelationId: {CorrelationId} - Bag not found: {BagNumber}", correlationId, bagNumber);
            return StatusCode(StatusCodes.Status404NotFound, ToErrorResponse(error, correlationId));
        }

        var document = getResult.Value!;
        return Ok(new BagRegisteredResponse
        {
            CustomerRefNumber = document.BagRegistration.CustomerRefNumber,
            BagNumber = document.BagRegistration.BagNumber,
            RegistrationStatus = document.RegistrationStatus,
            Description = "Bag Registration Found",
            BagLifecycleId = document.BagLifecycleId,
            RequestCorrelationId = correlationId
        });
    }

    [HttpDelete("{bagNumber}")]
    [ProducesResponseType(typeof(BagRegisteredResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status408RequestTimeout)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> DeleteBagRegistration(string bagNumber, CancellationToken cancellationToken)
    {
        var correlationId = HttpContext.Items[CorrelationConstants.HttpContextItemsKey]?.ToString() ?? "N/A";
        var clientId = User.GetClientId();

        if (string.IsNullOrEmpty(clientId))
        {
            logger.LogWarning("CorrelationId: {CorrelationId} - DELETE rejected, token missing clientId claim.", correlationId);
            return Unauthorized(new ErrorResponse
            {
                Title = "Unauthorized",
                Status = StatusCodes.Status401Unauthorized,
                CorrelationId = correlationId,
                Code = "ERR_MISSING_CLIENT_ID"
            });
        }

        var getResult = await repository.GetByBagNumberForClientAsync(bagNumber, clientId, cancellationToken);
        if (!getResult.IsSuccess)
        {
            logger.LogWarning("CorrelationId: {CorrelationId} - Delete rejected, bag not found: {BagNumber}", correlationId, bagNumber);
            return StatusCode(StatusCodes.Status404NotFound, ToErrorResponse(getResult.Error!, correlationId));
        }

        var existing = getResult.Value!;

        BagStatusRecord? status;
        try
        {
            status = await bagStatusFunctionClient.GetStatusAsync(bagNumber, correlationId, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("CorrelationId: {CorrelationId} - Bag status verification timed out for bag {BagNumber}", correlationId, bagNumber);
            return StatusCode(StatusCodes.Status408RequestTimeout, new ErrorResponse
            {
                Title = "Bag status verification timed out.",
                Status = StatusCodes.Status408RequestTimeout,
                CorrelationId = correlationId,
                Code = "ERR_STATUS_TIMEOUT"
            });
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "CorrelationId: {CorrelationId} - Unable to reach status Function for bag {BagNumber}", correlationId, bagNumber);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ErrorResponse
            {
                Title = "Unable to verify bag status.",
                Status = StatusCodes.Status503ServiceUnavailable,
                CorrelationId = correlationId,
                Code = "ERR_STATUS_UNAVAILABLE"
            });
        }

        if (status is null)
        {
            logger.LogWarning("CorrelationId: {CorrelationId} - Delete rejected, bag not found in status verification: {BagNumber}", correlationId, bagNumber);
            return StatusCode(StatusCodes.Status404NotFound, new ErrorResponse
            {
                Title = "Bag not found (status verification).",
                Status = StatusCodes.Status404NotFound,
                CorrelationId = correlationId,
                Code = "ERR_BAG_STATUS_NOT_FOUND"
            });
        }

        var outcome = BagCancellationDecision.Evaluate(status.intStatus, status.dtmTransportFetch);

        if (outcome == BagCancellationOutcome.AlreadyCancelled)
        {
            if (existing.RegistrationStatus != "Deleted")
            {
                existing.RegistrationStatus = "Deleted";
                existing.BagRegistration.ActionFlag = "Cancelled";
                await repository.UpdateAsync(existing, cancellationToken);
            }

            logger.LogWarning("CorrelationId: {CorrelationId} - Delete rejected, bag already cancelled: {BagNumber}", correlationId, bagNumber);
            return Conflict(new ErrorResponse
            {
                Title = "Bag already Cancelled",
                Status = StatusCodes.Status409Conflict,
                CorrelationId = correlationId,
                Code = "ERR_ALREADY_CANCELLED"
            });
        }

        if (outcome == BagCancellationOutcome.NotCancellable)
        {
            logger.LogWarning(
                "CorrelationId: {CorrelationId} - Delete rejected, bag {BagNumber} not cancellable (status={Status}, transportFetch={TransportFetch})",
                correlationId, bagNumber, status.intStatus, status.dtmTransportFetch);
            return BadRequest(new ErrorResponse
            {
                Title = "Bag already processed and cannot be cancelled at this stage.",
                Status = StatusCodes.Status400BadRequest,
                CorrelationId = correlationId,
                Code = "ERR_NOT_CANCELLABLE"
            });
        }

        existing.RegistrationStatus = "Deleted";
        existing.BagRegistration.ActionFlag = "Cancelled";
        existing.OperationHistory.Add(new OperationHistoryEntry
        {
            Action = "Delete",
            Timestamp = DateTime.UtcNow,
            BagLifecycleId = existing.BagLifecycleId,
            CorrelationId = correlationId,
            PerformedBy = clientId
        });

        var cancelPayload = BagCancellationOutboxPayload.From(
            existing.BagRegistration.CustomerRefNumber, existing.BagRegistration.BagNumber, $"API_{clientId}", DateTime.UtcNow, status);

        var outboxMessage = new OutboxMessageDocument
        {
            ClientId = clientId,
            Subject = "deleteBagRegistration",
            Payload = JsonSerializer.Serialize(cancelPayload, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            })
        };

        var updateResult = await repository.UpdateWithOutboxAsync(existing, outboxMessage, cancellationToken);
        if (!updateResult.IsSuccess)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ToErrorResponse(updateResult.Error!, correlationId));
        }

        logger.LogInformation("BagLifecycleId: {BagLifecycleId}, CorrelationId: {CorrelationId} - Bag cancelled successfully.",
            existing.BagLifecycleId, correlationId);

        return Ok(new BagRegisteredResponse
        {
            CustomerRefNumber = existing.BagRegistration.CustomerRefNumber,
            BagNumber = existing.BagRegistration.BagNumber,
            RegistrationStatus = existing.RegistrationStatus,
            Description = "Bag Cancelled Successfully.",
            BagLifecycleId = existing.BagLifecycleId,
            RequestCorrelationId = correlationId
        });
    }

    private static ErrorResponse ToErrorResponse(Error error, string correlationId) => new()
    {
        Title = error.Message,
        Status = error.Kind switch
        {
            ErrorKind.Conflict => StatusCodes.Status409Conflict,
            ErrorKind.Validation => StatusCodes.Status400BadRequest,
            ErrorKind.NotFound => StatusCodes.Status404NotFound,
            ErrorKind.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorKind.DownstreamUnavailable => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status500InternalServerError
        },
        CorrelationId = correlationId,
        Code = error.Code,
        Field = error.Field
    };
}