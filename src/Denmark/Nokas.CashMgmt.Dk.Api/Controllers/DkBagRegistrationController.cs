using System.Text;
using System.Text.Json;
using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Nokas.CashMgmt.Dk.Api.Configuration;
using Nokas.CashMgmt.Dk.Api.Documents;
using Nokas.CashMgmt.Dk.Shared.Documents;
using Nokas.CashMgmt.Dk.Api.Helpers;
using Nokas.CashMgmt.Dk.Api.Middleware;
using Nokas.CashMgmt.Dk.Api.Models;
using Nokas.CashMgmt.Dk.Api.Repositories;
using Nokas.CashMgmt.Dk.Api.Services;
using Nokas.CashMgmt.Dk.Api.Validation;
using Nokas.CashMgmt.Common.Correlation;
using Nokas.CashMgmt.Common.Errors;
using Nokas.CashMgmt.Dk.Shared.Repositories;

namespace Nokas.CashMgmt.Dk.Api.Controllers;

/// <summary>
/// Danish bag registration - a synchronous pass-through to ServiceNow.
///
/// The partner is told exactly what happened: 201 only when ServiceNow confirmed the
/// registration, and a clear error otherwise. Nothing is reported as registered on a guess.
/// The Cosmos <c>dk-registrations</c> container is a journal written AFTER the call, for
/// support and for the GET readback; it is never the source of truth and a failure to write it
/// never changes the answer given to the partner.
/// </summary>
[ApiController]
[Route("api/cash-mgmt/dk/bag-registration")]
[Authorize(Policy = AuthConfig.RegistrationRolePolicy)]
[Produces("application/json", "application/xml")]
public sealed class DkBagRegistrationController(
    IDkBagRegistrationRepository repository,
    IDkKardexMapRepository kardexMap,
    IServiceNowRegistrationClient serviceNow,
    IOptions<StubOptions> stubOptions,
    TelemetryClient telemetry,
    ILogger<DkBagRegistrationController> logger) : ControllerBase
{
    // Partner metadata belongs in the body's metaData object, which is validated and stored.
    // Headers are still captured and forwarded as a safety net for anything a partner sends
    // that way, minus infrastructure noise and anything sensitive.
    private static readonly HashSet<string> InfrastructureHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization", "Host", "Content-Length", "Connection", "Accept-Encoding",
        "Cache-Control", "Cookie", "Origin", "Referer", "Max-Forwards", "Via", "TE",
        "Upgrade", "Expect", "traceparent", "tracestate", "Disguised-Host",
        // ASP.NET Core and the Azure App Service host add these themselves - confirmed present
        // on live requests (2026-08-26), they are not anything a partner ever sent.
        "Request-Id", "Request-Context", "CLIENT-IP", "X-AppService-Proto"
    };

    private static bool IsInfrastructureHeader(string name) =>
        InfrastructureHeaders.Contains(name) ||
        name.StartsWith("X-Forwarded-", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("X-Azure-", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("X-ARR-", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("X-Original-", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("X-Waws-", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("X-Site-", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("WAS-", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("Sec-", StringComparison.OrdinalIgnoreCase);

    /// <summary>Registers a cash bag.</summary>
    /// <remarks>
    /// Expected body:
    ///
    ///     {
    ///       "bagRegistration": {
    ///         "customerRefNumber": "5790000123456",
    ///         "cashRegisterId": "POS-01",
    ///         "messageId": "0f8fad5b-d9cb-469f-a165-70867728950e",
    ///         "bagNumber": "3164696800",
    ///         "registrationDate": "2026-08-17",
    ///         "totalOrderAmount": "888.50",
    ///         "bagItemsLC": { "DKK_500": "1", "DKK_200": "1" }
    ///       },
    ///       "metaData": { "companyId": "3214", "branchId": "9543" }
    ///     }
    ///
    /// metaData is optional and free-form: any name/value pairs of your own (max 20 entries,
    /// names 50 chars, values 200 chars, all values JSON strings). It is stored and passed on to
    /// NOKAS's backend, and is never used to decide what you can see.
    ///
    /// Every bagRegistration field is mandatory except bagItemsLC, and all must be JSON strings. Each bagItemsLC
    /// value is a quantity of that denomination, not an amount. Unknown fields are rejected.
    ///
    /// The registration is sent to NOKAS's backend before the answer is returned. 201 means the
    /// backend confirmed it. On 502/503 nothing is confirmed: after a 503 the bag may or may not
    /// exist, so retry with the same messageId or contact NOKAS.
    /// </remarks>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(DkBagRegistrationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Post(CancellationToken cancellationToken)
    {
        var correlationId = CorrelationId();
        var clientId = User.GetClientId();

        if (string.IsNullOrEmpty(clientId))
        {
            logger.LogWarning("CorrelationId: {CorrelationId} - Token carried no azp/appid claim.", correlationId);
            return Unauthorized(Error("Client identity missing from token.", StatusCodes.Status401Unauthorized, "UNAUTHORIZED", correlationId));
        }

        var body = await ReadBodyAsync();
        var payloadBytes = Encoding.UTF8.GetByteCount(body);
        var bagLifecycleId = Guid.NewGuid().ToString();
        var receivedAt = DateTime.UtcNow;
        var requestHeaders = Request.Headers
            .Where(h => !IsInfrastructureHeader(h.Key))
            .ToDictionary(h => h.Key, h => h.Value.ToString());

        var validation = DkBagRegistrationValidator.Validate(body, stubOptions.Value.EnforceDenominationTotal);

        TrackReceived(clientId, correlationId, bagLifecycleId, payloadBytes, validation);

        if (!validation.IsValid)
        {
            logger.LogWarning(
                "CorrelationId: {CorrelationId} - DK registration rejected. BagLifecycleId={BagLifecycleId}, Errors={Errors}",
                correlationId, bagLifecycleId, validation.JoinedMessages);

            await JournalAsync(BuildDocument(bagLifecycleId, clientId, correlationId, receivedAt, body, payloadBytes, requestHeaders, validation, accepted: false, serviceNowRecord: null));

            return BadRequest(new ErrorResponse
            {
                Title = "The payload does not match the Danish bag registration contract.",
                Status = StatusCodes.Status400BadRequest,
                CorrelationId = correlationId,
                Code = "VALIDATION_ERROR",
                Field = validation.Failures[0].Field,
                Exception = validation.JoinedMessages
            });
        }

        var fields = validation.Fields!;

        // Tenancy check BEFORE the call: a kardex owned by another client must never reach
        // ServiceNow. The claim itself is only made AFTER ServiceNow confirms, so a failed
        // registration cannot lock a kardex to a client that never got one.
        string? owner;
        try
        {
            owner = await kardexMap.TryGetOwnerAsync(fields.CustomerRefNumber, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex,
                "CorrelationId: {CorrelationId} - Could not check the owner of customerRefNumber {CustomerRefNumber}.",
                correlationId, fields.CustomerRefNumber);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, Error(
                "The registration could not be processed right now. Nothing was registered. Please retry.",
                StatusCodes.Status503ServiceUnavailable, "ERR_SERVICE_UNAVAILABLE", correlationId));
        }

        if (owner is not null && owner != clientId)
        {
            logger.LogWarning(
                "CorrelationId: {CorrelationId} - DK registration rejected: customerRefNumber {CustomerRefNumber} belongs to another client.",
                correlationId, fields.CustomerRefNumber);

            await JournalAsync(BuildDocument(bagLifecycleId, clientId, correlationId, receivedAt, body, payloadBytes, requestHeaders, validation, accepted: false, serviceNowRecord: null));

            return StatusCode(StatusCodes.Status409Conflict, Error(
                $"customerRefNumber '{fields.CustomerRefNumber}' is already assigned to a different client.",
                StatusCodes.Status409Conflict, "ERR_CUSTOMER_REF_CONFLICT", correlationId));
        }

        // The call is deliberately not tied to the request's cancellation token: if the partner
        // disconnects mid-call, ServiceNow may still create the record, and the journal must
        // still capture the outcome. The client applies its own timeout.
        var result = await serviceNow.RegisterAsync(
            new ServiceNowRegistrationRequest(clientId, correlationId, fields.BagNumber, body, requestHeaders),
            CancellationToken.None);

        var succeeded = result.Outcome == ServiceNowOutcome.Success;

        await JournalAsync(BuildDocument(bagLifecycleId, clientId, correlationId, receivedAt, body, payloadBytes, requestHeaders, validation, accepted: succeeded, serviceNowRecord: result.Record));

        if (!succeeded)
        {
            return MapFailure(result, correlationId);
        }

        try
        {
            var claim = await kardexMap.ClaimOrVerifyAsync(fields.CustomerRefNumber, clientId, "registration", CancellationToken.None);
            if (claim == KardexClaimOutcome.OwnedByDifferentClient)
            {
                // Lost a race with another client's registration for the same new kardex. The
                // bag IS registered in ServiceNow, so the partner is told so; support can see this.
                logger.LogWarning(
                    "CorrelationId: {CorrelationId} - customerRefNumber {CustomerRefNumber} was claimed by another client while this registration was in flight.",
                    correlationId, fields.CustomerRefNumber);
            }
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex,
                "CorrelationId: {CorrelationId} - Registered bag {BagNumber} but could not record the claim for {CustomerRefNumber}.",
                correlationId, fields.BagNumber, fields.CustomerRefNumber);
        }

        logger.LogInformation(
            "CorrelationId: {CorrelationId} - DK registration confirmed by ServiceNow. BagLifecycleId={BagLifecycleId}, BagNumber={BagNumber}, MessageId={MessageId}, Record={RecordId}",
            correlationId, bagLifecycleId, fields.BagNumber, fields.MessageId, result.Record.SysId ?? "(none)");

        var response = new DkBagRegistrationResponse
        {
            CustomerRefNumber = fields.CustomerRefNumber,
            BagNumber = fields.BagNumber,
            MessageId = fields.MessageId,
            // ServiceNow's own confirmation text, passed through unchanged.
            Description = result.Message ?? "Bag registered.",
            RegistrationStatus = "Registered",
            BagLifecycleId = bagLifecycleId,
            RequestCorrelationId = correlationId,
            Warnings = validation.Warnings.Count > 0 ? [.. validation.Warnings] : null
        };

        return Created($"/api/cash-mgmt/dk/bag-registration/{fields.BagNumber}", response);
    }

    /// <summary>Retrieves the most recent confirmed registration for a bag number.</summary>
    [HttpGet("{bagNumber}")]
    [ProducesResponseType(typeof(DkBagRegistrationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(string bagNumber, CancellationToken cancellationToken)
    {
        var correlationId = CorrelationId();
        var clientId = User.GetClientId();

        if (string.IsNullOrEmpty(clientId))
        {
            return Unauthorized(Error("Client identity missing from token.", StatusCodes.Status401Unauthorized, "UNAUTHORIZED", correlationId));
        }

        var document = await repository.GetLatestByBagNumberAsync(bagNumber, clientId, cancellationToken);

        if (document is null)
        {
            logger.LogWarning("CorrelationId: {CorrelationId} - No DK registration found for bag number {BagNumber}.", correlationId, bagNumber);
            return NotFound(Error(
                $"No bag found with bag number '{bagNumber}' for this client.",
                StatusCodes.Status404NotFound, "ERR_BAG_NOT_FOUND", correlationId));
        }

        return Ok(new DkBagRegistrationResponse
        {
            CustomerRefNumber = document.CustomerRefNumber!,
            BagNumber = document.BagNumber!,
            MessageId = document.MessageId!,
            Description = stubOptions.Value.FoundDescription,
            RegistrationStatus = "Registered",
            BagLifecycleId = document.BagLifecycleId,
            RequestCorrelationId = correlationId
        });
    }

    // What the partner is told for every way ServiceNow can fail. Wording about ServiceNow's
    // own reason is relayed only where the partner can act on it (a refusal or a duplicate);
    // faults on our side of the integration are reported generically, with the correlation id
    // for support.
    private IActionResult MapFailure(ServiceNowRegistrationResult result, string correlationId)
    {
        switch (result.Outcome)
        {
            case ServiceNowOutcome.Duplicate:
                return StatusCode(StatusCodes.Status409Conflict, Error(
                    result.Message ?? "A bag with this bag number is already registered.",
                    StatusCodes.Status409Conflict, "ERR_DUPLICATE_BAG", correlationId));

            case ServiceNowOutcome.Rejected:
                return BadRequest(Error(
                    result.Message ?? "The registration was refused by the backend system.",
                    StatusCodes.Status400BadRequest, "ERR_REGISTRATION_REJECTED", correlationId));

            case ServiceNowOutcome.BackendError:
                return StatusCode(StatusCodes.Status502BadGateway, Error(
                    "The registration could not be completed by the backend system. The bag was not registered. Contact NOKAS support with the correlationId if this continues.",
                    StatusCodes.Status502BadGateway, "ERR_BACKEND_ERROR", correlationId));

            default:
                return StatusCode(StatusCodes.Status503ServiceUnavailable, Error(
                    "The backend system did not respond in time. The bag may or may not have been registered. Retry with the same messageId, or contact NOKAS support with the correlationId.",
                    StatusCodes.Status503ServiceUnavailable, "ERR_BACKEND_UNAVAILABLE", correlationId));
        }
    }

    // Journal writes are best-effort: the answer to the partner is already decided by the time
    // this runs, and a Cosmos hiccup must not change it.
    private async Task JournalAsync(DkBagRegistrationDocument document)
    {
        try
        {
            await repository.SaveAsync(document, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex,
                "CorrelationId: {CorrelationId} - Failed to journal DK registration {BagLifecycleId}.",
                document.CorrelationId, document.BagLifecycleId);
        }
    }

    private DkBagRegistrationDocument BuildDocument(
        string bagLifecycleId,
        string clientId,
        string correlationId,
        DateTime receivedAt,
        string body,
        int payloadBytes,
        Dictionary<string, string> requestHeaders,
        DkValidationResult validation,
        bool accepted,
        ServiceNowRegistrationRecord? serviceNowRecord)
    {
        var parsed = TryParseJson(body);
        var withinBudget = payloadBytes <= stubOptions.Value.MaxCosmosPayloadBytes;
        var fields = validation.Fields;

        return new DkBagRegistrationDocument
        {
            Id = bagLifecycleId,
            ClientId = clientId,
            BagLifecycleId = bagLifecycleId,
            CorrelationId = correlationId,
            ReceivedAtUtc = receivedAt,
            Accepted = accepted,
            ValidationErrors = validation.Failures.Count > 0
                ? [.. validation.Failures.Select(f => new DocumentValidationError { Field = f.Field, Message = f.Message })]
                : null,
            ValidationWarnings = validation.Warnings.Count > 0 ? [.. validation.Warnings] : null,
            CustomerRefNumber = fields?.CustomerRefNumber,
            CashRegisterId = fields?.CashRegisterId,
            MessageId = fields?.MessageId,
            BagNumber = fields?.BagNumber,
            RegistrationDate = fields?.RegistrationDate.ToString("yyyy-MM-dd"),
            TotalOrderAmount = fields?.TotalOrderAmount,
            DenominationTotal = validation.DenominationTotal,
            ContentType = Request.ContentType,
            PayloadBytes = payloadBytes,
            PayloadIsJson = parsed is not null,
            Payload = withinBudget ? parsed : null,
            PayloadText = withinBudget && parsed is null ? body : null,
            PayloadOmittedReason = withinBudget ? null : "Payload exceeded the Cosmos size budget; the full body is in Blob storage.",
            ArchiveBlobName = HttpContext.Items[PayloadArchivingMiddleware.ArchivedBlobNameItemKey]?.ToString(),
            MetaData = fields?.MetaData,
            RequestHeaders = requestHeaders,
            ServiceNow = serviceNowRecord
        };
    }

    private void TrackReceived(string clientId, string correlationId, string bagLifecycleId, int payloadBytes, DkValidationResult validation)
    {
        var fields = validation.Fields;

        telemetry.TrackEvent("DkBagRegistrationReceived", new Dictionary<string, string>
        {
            ["clientId"] = clientId,
            ["correlationId"] = correlationId,
            ["bagLifecycleId"] = bagLifecycleId,
            ["payloadValid"] = validation.IsValid.ToString(),
            ["bagNumber"] = fields?.BagNumber ?? "(invalid)",
            ["messageId"] = fields?.MessageId ?? "(invalid)",
            ["customerRefNumber"] = fields?.CustomerRefNumber ?? "(invalid)",
            ["cashRegisterId"] = fields?.CashRegisterId ?? "(invalid)",
            ["payloadBytes"] = payloadBytes.ToString(),
            ["validationErrorCount"] = validation.Failures.Count.ToString(),
            // The failing field names, not their values, so a recurring partner mistake is
            // visible in App Insights without storing payload content.
            ["validationErrorFields"] = validation.Failures.Count > 0
                ? string.Join(",", validation.Failures.Select(f => f.Field))
                : "(none)",
            ["warningCount"] = validation.Warnings.Count.ToString(),
            // Names only, not values - shows what partners actually send without storing their content here.
            ["metaDataFields"] = fields?.MetaData is { Count: > 0 } m ? string.Join(",", m.Keys) : "(none)"
        });
    }

    private string CorrelationId() =>
        HttpContext.Items[CorrelationConstants.HttpContextItemsKey]?.ToString() ?? HttpContext.TraceIdentifier;

    private static ErrorResponse Error(string title, int status, string code, string correlationId) =>
        new() { Title = title, Status = status, Code = code, CorrelationId = correlationId };

    private async Task<string> ReadBodyAsync()
    {
        // The archiving middleware has already buffered and rewound the stream; enabling
        // buffering again is harmless and keeps this working if that middleware is bypassed.
        Request.EnableBuffering();
        Request.Body.Position = 0;

        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        Request.Body.Position = 0;
        return body;
    }

    private static JsonElement? TryParseJson(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            // Clone detaches the element from the JsonDocument so it outlives the using block.
            using var document = JsonDocument.Parse(body);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
