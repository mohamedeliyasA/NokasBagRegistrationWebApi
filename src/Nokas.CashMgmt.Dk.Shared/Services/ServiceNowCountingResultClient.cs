using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Nokas.CashMgmt.Dk.Shared.Configuration;

using Microsoft.Extensions.Logging;

namespace Nokas.CashMgmt.Dk.Shared.Services;

public static class ServiceNowHttp
{
    public const string ClientName = "servicenow";
}

/// <summary>
/// Calls ServiceNow's counting_results feed for a window of LAST-MODIFIED time. Reply shape
/// observed on this endpoint: <c>{ "result": { "result": [ … ], "status": "success" } }</c>; the
/// plain Table API shape <c>{ "result": [ … ] }</c> is accepted too.
/// </summary>
public sealed class ServiceNowCountingResultClient(
    IHttpClientFactory httpClientFactory,
    ServiceNowCredentialHolder credentialHolder,
    IOptions<ServiceNowOptions> options,
    ServiceNowClock clock,
    ILogger<ServiceNowCountingResultClient> logger) : IServiceNowCountingResultClient
{
    public async Task<IReadOnlyList<JsonElement>?> FetchModifiedBetweenAsync(
        DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
    {
        var credential = credentialHolder.Credential;
        if (credential is null)
        {
            logger.LogCritical("Cannot fetch ServiceNow counting results: the ServiceNow credential is missing or unparseable.");
            return null;
        }

        var from = clock.ToQueryString(fromUtc);
        var to = clock.ToQueryString(toUtc);
        var url = $"{credential.BaseUrl}{options.Value.CountingResultPath}" +
                  $"?startDate={Uri.EscapeDataString(from)}&endDate={Uri.EscapeDataString(to)}";

        try
        {
            using var client = httpClientFactory.CreateClient(ServiceNowHttp.ClientName);
            using var request = new HttpRequestMessage(HttpMethod.Get, url);

            var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{credential.Username}:{credential.Password}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);

            using var response = await client.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "ServiceNow counting_results ({From} .. {To} {Zone}) returned {StatusCode}. Body={Body}",
                    from, to, clock.ZoneId, (int)response.StatusCode, Truncate(body, 500));
                return null;
            }

            using var document = JsonDocument.Parse(body);

            var result = default(JsonElement);
            var found = document.RootElement.TryGetProperty("result", out result);
            if (found && result.ValueKind == JsonValueKind.Object)
            {
                found = result.TryGetProperty("result", out result);
            }

            if (!found || result.ValueKind != JsonValueKind.Array)
            {
                logger.LogWarning(
                    "ServiceNow counting_results ({From} .. {To}) response had no 'result' array. Body={Body}",
                    from, to, Truncate(body, 1500));
                return null;
            }

            return [.. result.EnumerateArray().Select(e => e.Clone())];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch ServiceNow counting_results ({From} .. {To}).", from, to);
            return null;
        }
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max] + "…";
}
