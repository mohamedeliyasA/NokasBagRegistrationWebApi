using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Options;
using Nokas.CashMgmt.Dk.BagRegistration.Api.Configuration;
using Nokas.CashMgmt.Dk.BagRegistration.Api.Documents;
using Nokas.CashMgmt.Dk.Shared.Documents;
using Nokas.CashMgmt.Dk.BagRegistration.Api.Repositories;
using Nokas.CashMgmt.Dk.Shared.Configuration;
using Nokas.CashMgmt.Dk.Shared.Services;

namespace Nokas.CashMgmt.Dk.BagRegistration.Api.Services;

public enum ServiceNowOutcome
{
    /// <summary>ServiceNow confirmed the registration.</summary>
    Success,

    /// <summary>ServiceNow reported the bag as already registered.</summary>
    Duplicate,

    /// <summary>ServiceNow refused the request on its merits (a 4xx we can relay).</summary>
    Rejected,

    /// <summary>ServiceNow answered, but with an error we cannot interpret or a server fault.</summary>
    BackendError,

    /// <summary>No usable answer: timeout, connection failure, or no credential.</summary>
    Unavailable
}

public sealed record ServiceNowRegistrationRequest(
    string ClientId,
    string CorrelationId,
    string BagNumber,
    string Payload,
    IReadOnlyDictionary<string, string> RequestHeaders);

public sealed record ServiceNowRegistrationResult(
    ServiceNowOutcome Outcome,
    ServiceNowRegistrationRecord Record,
    // ServiceNow's own wording (success message, or its refusal reason). Null if it gave none.
    string? Message);

public interface IServiceNowRegistrationClient
{
    /// <summary>Never throws: every failure is expressed as an outcome.</summary>
    Task<ServiceNowRegistrationResult> RegisterAsync(ServiceNowRegistrationRequest request, CancellationToken cancellationToken);
}

/// <summary>Pure parsing and classification of ServiceNow's registration reply.</summary>
public static class ServiceNowRegistrationResponse
{
    public sealed record Parsed(string? Status, string? RecordId, string? Message, bool BusinessFailure);

    /// <summary>
    /// Handles both observed shapes:
    ///   success  { "result": { "status": "success", "message_id"|"sys_id": "...", "message": "..." } }
    ///   failure  { "error": { "message": "..." }, "status": "failure" }
    /// </summary>
    public static Parsed? Parse(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            string? status = null, id = null, message = null;
            var businessFailure = false;

            if (root.TryGetProperty("result", out var result) && result.ValueKind == JsonValueKind.Object)
            {
                status = ReadString(result, "status");
                id = ReadString(result, "sys_id") ?? ReadString(result, "message_id");
                message = ReadString(result, "message");

                // ServiceNow reports its own business-rule refusals inside "result" with
                // status "failed" (sometimes under HTTP 500), e.g. "The bag label number must
                // belong to the account of the registration." - as opposed to platform errors,
                // which arrive under "error". Only the former is something the partner can act on.
                businessFailure = message is not null && status is not null
                    && !status.Equals("success", StringComparison.OrdinalIgnoreCase);
            }

            status ??= ReadString(root, "status");

            if (root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
            {
                message ??= ReadString(error, "message");
            }

            return new Parsed(status, id, message, businessFailure);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static ServiceNowOutcome Classify(int httpStatus, Parsed? parsed)
    {
        if (httpStatus is >= 200 and < 300)
        {
            // A 2xx we cannot read, or one that says anything other than success, is not a
            // confirmation - never tell a partner "registered" on a guess.
            return parsed?.Status is not null && parsed.Status.Equals("success", StringComparison.OrdinalIgnoreCase)
                ? ServiceNowOutcome.Success
                : parsed?.Status is not null ? ServiceNowOutcome.Rejected : ServiceNowOutcome.BackendError;
        }

        if (httpStatus is not (401 or 403) && parsed is { BusinessFailure: true })
        {
            // ServiceNow has no error codes, so these are recognised by wording until they give us
            // something machine-readable.
            //
            // Their own faults arrive in the same shape as a genuine refusal ("Internal error.
            // Please contact Nokas..."). Relaying that as a 400 would tell a partner their request
            // was wrong when nothing they can change would help, so it is a backend fault.
            if (parsed.Message!.Contains("internal error", StringComparison.OrdinalIgnoreCase))
            {
                return ServiceNowOutcome.BackendError;
            }

            return parsed.Message.Contains("already been registered", StringComparison.OrdinalIgnoreCase)
                ? ServiceNowOutcome.Duplicate
                : ServiceNowOutcome.Rejected;
        }

        return httpStatus switch
        {
            409 => ServiceNowOutcome.Duplicate,
            400 or 404 or 422 => ServiceNowOutcome.Rejected,
            // 401/403 mean our credential or source registration is wrong - the partner cannot
            // act on that, so it is a backend fault from their point of view, as is any 5xx.
            _ => ServiceNowOutcome.BackendError
        };
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}

/// <summary>
/// Registers a bag in ServiceNow synchronously. The caller answers the partner from the
/// outcome, so what the partner is told is what actually happened.
///
/// There is deliberately no retry here: a retried POST that already succeeded on the first
/// attempt would create a duplicate registration. Retrying is the partner's decision, keyed on
/// messageId.
/// </summary>
public sealed class ServiceNowRegistrationClient(
    IHttpClientFactory httpClientFactory,
    ServiceNowCredentialHolder credentialHolder,
    IOptions<ServiceNowOptions> options,
    IDkCustomerDirectoryRepository customerDirectory,
    TelemetryClient telemetry,
    ILogger<ServiceNowRegistrationClient> logger) : IServiceNowRegistrationClient
{
    public async Task<ServiceNowRegistrationResult> RegisterAsync(
        ServiceNowRegistrationRequest request, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var record = new ServiceNowRegistrationRecord { ForwardedAtUtc = DateTime.UtcNow };

        var credential = credentialHolder.Credential;
        if (credential is null)
        {
            logger.LogCritical("CorrelationId: {CorrelationId} - ServiceNow credential is missing or unparseable; cannot register.", request.CorrelationId);
            return Finish(request, ServiceNowOutcome.Unavailable, record with { Outcome = "unavailable", Error = "credential missing" }, null, stopwatch);
        }

        try
        {
            var customerName = await ResolveCustomerNameAsync(request.ClientId, cancellationToken);
            var payload = BuildOutboundPayload(request.Payload, request.ClientId, customerName);

            using var client = httpClientFactory.CreateClient(ServiceNowHttp.ClientName);
            using var message = new HttpRequestMessage(HttpMethod.Post, credential.BaseUrl + options.Value.RegistrationPath)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };

            var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{credential.Username}:{credential.Password}"));
            message.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
            var forwarded = AddPartnerHeaders(message, request.RequestHeaders);

            using var response = await client.SendAsync(message, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            var parsed = ServiceNowRegistrationResponse.Parse(body);
            var outcome = ServiceNowRegistrationResponse.Classify((int)response.StatusCode, parsed);

            record = record with
            {
                Outcome = OutcomeLabel(outcome),
                HttpStatus = (int)response.StatusCode,
                SysId = parsed?.RecordId,
                Message = parsed?.Message,
                ForwardedHeaders = forwarded,
                CustomerName = customerName,
                RawBody = outcome == ServiceNowOutcome.Success ? null : Truncate(body, 1000)
            };

            return Finish(request, outcome, record, parsed?.Message, stopwatch);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "CorrelationId: {CorrelationId} - ServiceNow registration call failed for bag {BagNumber}.",
                request.CorrelationId, request.BagNumber);
            return Finish(request, ServiceNowOutcome.Unavailable,
                record with { Outcome = "unavailable", Error = ex.GetType().Name + ": " + Truncate(ex.Message, 300) }, null, stopwatch);
        }
    }

    private ServiceNowRegistrationResult Finish(
        ServiceNowRegistrationRequest request, ServiceNowOutcome outcome, ServiceNowRegistrationRecord record, string? message, Stopwatch stopwatch)
    {
        stopwatch.Stop();
        record = record with { DurationMs = (int)stopwatch.ElapsedMilliseconds };

        if (outcome == ServiceNowOutcome.Success)
        {
            logger.LogInformation(
                "CorrelationId: {CorrelationId} - ServiceNow registered bag {BagNumber}. Record={RecordId}, {DurationMs}ms",
                request.CorrelationId, request.BagNumber, record.SysId ?? "(none)", record.DurationMs);
        }
        else
        {
            logger.LogWarning(
                "CorrelationId: {CorrelationId} - ServiceNow registration for bag {BagNumber} ended as {Outcome}. HttpStatus={HttpStatus}, Body={Body}",
                request.CorrelationId, request.BagNumber, record.Outcome, record.HttpStatus?.ToString() ?? "(none)", record.RawBody ?? record.Error ?? "(none)");
        }

        telemetry.TrackEvent("DkServiceNowForward", new Dictionary<string, string>
        {
            ["clientId"] = request.ClientId,
            ["correlationId"] = request.CorrelationId,
            ["bagNumber"] = request.BagNumber,
            ["outcome"] = record.Outcome ?? "unknown",
            ["httpStatus"] = record.HttpStatus?.ToString() ?? "(none)",
            ["recordId"] = record.SysId ?? "(none)",
            ["durationMs"] = record.DurationMs?.ToString() ?? "(none)",
            ["error"] = record.Error ?? "(none)",
            ["forwardedHeaders"] = record.ForwardedHeaders is { Count: > 0 } ? string.Join(",", record.ForwardedHeaders) : "(none)",
            ["customerName"] = record.CustomerName ?? "(not in directory)"
        });

        return new ServiceNowRegistrationResult(outcome, record, message);
    }

    private static string OutcomeLabel(ServiceNowOutcome outcome) => outcome switch
    {
        ServiceNowOutcome.Success => "success",
        ServiceNowOutcome.Duplicate => "duplicate",
        ServiceNowOutcome.Rejected => "rejected",
        ServiceNowOutcome.BackendError => "backend-error",
        _ => "unavailable"
    };

    private async Task<string?> ResolveCustomerNameAsync(string clientId, CancellationToken cancellationToken)
    {
        try
        {
            var name = await customerDirectory.GetCustomerNameAsync(clientId, cancellationToken);
            if (name is null)
            {
                logger.LogWarning("ClientId {ClientId} has no entry in the DK customer directory - sending customerId only.", clientId);
            }

            return name;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to resolve the customer name for clientId {ClientId}.", clientId);
            return null;
        }
    }

    // Adds a "nokasSource" object alongside the partner's original "bagRegistration" object.
    // Never partner-supplied - it comes from the validated token - so it cannot be spoofed.
    private static string BuildOutboundPayload(string originalPayload, string clientId, string? customerName)
    {
        using var document = JsonDocument.Parse(originalPayload);
        using var stream = new MemoryStream();

        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();

            foreach (var property in document.RootElement.EnumerateObject())
            {
                property.WriteTo(writer);
            }

            writer.WriteStartObject("nokasSource");
            writer.WriteString("customerId", clientId);
            if (customerName is not null)
            {
                writer.WriteString("customerName", customerName);
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    // Content-Type is already set on the StringContent; adding it again as a header is invalid.
    private static IReadOnlyList<string> AddPartnerHeaders(HttpRequestMessage request, IReadOnlyDictionary<string, string> headers)
    {
        var forwarded = new List<string>();

        foreach (var (name, value) in headers)
        {
            if (string.Equals(name, "Content-Type", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (request.Headers.TryAddWithoutValidation(name, value))
            {
                forwarded.Add(name);
            }
        }

        return forwarded;
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max] + "…";
}
