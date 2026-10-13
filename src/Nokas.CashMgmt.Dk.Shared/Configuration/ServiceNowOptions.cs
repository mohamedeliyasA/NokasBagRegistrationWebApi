using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nokas.CashMgmt.Dk.Shared.Configuration;

public sealed class ServiceNowOptions
{
    /// <summary>
    /// Appended to the credential's baseUrl. Generic as of 2026-08-27 - ServiceNow removed the
    /// customer segment from the path at our request, so this is no longer per-customer.
    /// </summary>
    public string RegistrationPath { get; init; } = "/bag_registration";

    /// <summary>
    /// ServiceNow's counting-result feed. Confirmed to accept startDate/endDate/state/
    /// customerRefNumber, all optional - but as of 2026-09 the date-range filter is proven
    /// on the registered date.
    /// </summary>
    public string CountingResultPath { get; init; } = "/counting_results/dk";

    public int TimeoutSeconds { get; init; } = 10;

    public CountingResultSyncOptions CountingResultSync { get; init; } = new();
}

/// <summary>
/// Incremental sync. ServiceNow's <c>startDate</c>/<c>endDate</c> filter moved to the
/// LAST-MODIFIED time (2026-09-25), so each run asks only for what changed since the previous
/// run instead of re-reading a wide registration window.
/// </summary>
public sealed class CountingResultSyncOptions
{
    public bool Enabled { get; init; } = true;

    public int PollIntervalMinutes { get; init; } = 2;

    /// <summary>
    /// How far back before the stored watermark each run starts. Absorbs clock differences
    /// between us and ServiceNow and any row written moments after a window closed; re-reading a
    /// row is free because writes are idempotent upserts, missing one is not.
    /// </summary>
    public int OverlapMinutes { get; init; } = 10;

    /// <summary>Window for the very first run, before any watermark exists.</summary>
    public int InitialLookbackDays { get; init; } = 120;

    /// <summary>
    /// The zone ServiceNow's timestamps are in. Their API sends "yyyy-MM-dd HH:mm:ss" with no
    /// offset, and a bag registered at 06:14 UTC came back as 08:14 - i.e. Danish local time, not
    /// UTC. With a two-minute window a wrong zone means fetching nothing at all, silently, so
    /// this is configurable rather than assumed.
    /// </summary>
    public string TimeZone { get; init; } = "Central European Standard Time";
}

/// <summary>
/// Connection details for ServiceNow, held as a single Key Vault secret
/// (<c>ServiceNow--Dk--Credential</c>) so that a rotation is one value to change.
/// </summary>
public sealed record ServiceNowCredential(string BaseUrl, string Username, string Password)
{
    public static ServiceNowCredential? TryParse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<Raw>(json);
            if (parsed is null ||
                string.IsNullOrWhiteSpace(parsed.BaseUrl) ||
                string.IsNullOrWhiteSpace(parsed.Username) ||
                string.IsNullOrWhiteSpace(parsed.Password))
            {
                return null;
            }

            return new ServiceNowCredential(parsed.BaseUrl.TrimEnd('/'), parsed.Username, parsed.Password);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record Raw(
        [property: JsonPropertyName("baseUrl")] string? BaseUrl,
        [property: JsonPropertyName("username")] string? Username,
        [property: JsonPropertyName("password")] string? Password);
}

/// <summary>
/// Wraps the credential so DI can hold "no credential" as a legitimate state. Registering a
/// nullable singleton directly would throw on resolution when the factory returns null, which
/// would take the whole app down over a missing shadow-mode secret.
/// </summary>
public sealed class ServiceNowCredentialHolder(ServiceNowCredential? credential)
{
    public ServiceNowCredential? Credential { get; } = credential;
}
