using System.Text.Json;
using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nokas.CashMgmt.BagRegistration.Api.Configuration;
using Nokas.CashMgmt.BagRegistration.Api.Documents;
using Nokas.CashMgmt.BagRegistration.Api.Helpers;
using Nokas.CashMgmt.BagRegistration.Api.Mapping;
using Nokas.CashMgmt.BagRegistration.Api.Models;
using Nokas.CashMgmt.BagRegistration.Api.Repositories;
using Nokas.CashMgmt.BagRegistration.Api.Services;
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
            Payload = JsonSerializer.Serialize(request, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
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