using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nokas.CashMgmt.Dk.Shared.Configuration;
using Nokas.CashMgmt.Dk.Shared.Documents;
using Nokas.CashMgmt.Dk.Shared.Repositories;
using Nokas.CashMgmt.Dk.Shared.Services;

namespace Nokas.CashMgmt.Dk.Func.Services;

/// <summary>
/// Pulls ServiceNow's counting_results into Cosmos so the partner-facing API can serve them
/// without ever calling ServiceNow (sync-and-serve).
///
/// Incremental: ServiceNow's date filter works on LAST-MODIFIED time, so each run asks only for
/// rows changed since the previous run's watermark, minus an overlap. The watermark advances
/// only after a run completes; an unusable reply leaves it alone so the next run re-reads the
/// same window rather than stepping over data it never saw.
///
/// Deletions are deliberately NOT detected. An incremental feed cannot tell "unchanged" from
/// "deleted", so removing a stored row would mean guessing. Cancelled bags arrive as a state
/// change, which is the case that actually matters.
///
/// Ownership: every ServiceNow row carries the <c>clientID</c> we sent when the bag was
/// registered (from the validated token, never partner-supplied), so rows are stored under it
/// directly. A row without a clientID falls back to the kardex owner map; if that finds no owner
/// either, the row is skipped rather than guessed at.
///
/// This runs as a timer-triggered Function rather than inside the API so that exactly one
/// instance syncs however many instances serve partners, and so a slow sync cannot affect them.
/// </summary>
public sealed class DkCountingResultSyncService(
    IServiceNowCountingResultClient serviceNowClient,
    IDkKardexMapRepository kardexMap,
    IDkCountingResultRepository countingResults,
    IOptions<ServiceNowOptions> options,
    ServiceNowClock clock,
    ILogger<DkCountingResultSyncService> logger)
{
    /// <summary>Last run's counters, for the Function to log and emit as telemetry.</summary>
    public IReadOnlyDictionary<string, string> Telemetry { get; private set; } = new Dictionary<string, string>();

    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value.CountingResultSync;

        var now = DateTime.UtcNow;
        var watermark = await countingResults.GetWatermarkAsync(cancellationToken);
        var from = watermark is null
            ? now.AddDays(-settings.InitialLookbackDays)
            : watermark.Value.AddMinutes(-settings.OverlapMinutes);

        if (from >= now)
        {
            return;
        }

        var rows = await serviceNowClient.FetchModifiedBetweenAsync(from, now, cancellationToken);
        if (rows is null)
        {
            logger.LogWarning(
                "DK counting-result sync skipped: ServiceNow did not return a usable reply. The watermark stays at {Watermark:O}.",
                watermark);
            return;
        }

        var upserted = 0;
        var skippedUnowned = 0;
        var skippedInvalid = 0;

        foreach (var row in rows)
        {
            var parsed = TryParseRow(row);
            if (parsed is null)
            {
                skippedInvalid++;
                continue;
            }

            var owner = parsed.ClientId;
            if (owner is null && !string.IsNullOrWhiteSpace(parsed.CustomerRefNumber))
            {
                owner = await kardexMap.TryGetOwnerAsync(parsed.CustomerRefNumber, cancellationToken);
            }

            if (owner is null)
            {
                skippedUnowned++;
                logger.LogInformation(
                    "Skipping ServiceNow row {ServiceNowNumber}: no clientID on the row and kardex {CustomerRefNumber} has no owner.",
                    parsed.ServiceNowNumber, parsed.CustomerRefNumber ?? "(none)");
                continue;
            }

            var bucket = DkCountingResultStatusMapper.ToBucket(parsed.State);
            if (bucket == DkCountingResultStatusMapper.Unknown)
            {
                logger.LogWarning(
                    "ServiceNow row {ServiceNowNumber} has an unmapped state '{State}' - stored as 'unknown'.",
                    parsed.ServiceNowNumber, parsed.State);
            }

            // ServiceNow reports countedAmount 0 on bags that have not been counted; only trust
            // it once the row is in the counted state.
            var counted = bucket == DkCountingResultStatusMapper.Counted ? parsed.CountedAmount : null;

            await countingResults.UpsertAsync(new DkCountingResultDocument
            {
                Id = parsed.ServiceNowNumber,
                ClientId = owner,
                CustomerRefNumber = parsed.CustomerRefNumber ?? string.Empty,
                BagNumber = parsed.BagNumber,
                ServiceNowNumber = parsed.ServiceNowNumber,
                Status = bucket,
                RawState = parsed.State,
                DeclaredAmount = parsed.DeclaredAmount,
                CountedAmount = counted,
                DeviationAmount = parsed.DeclaredAmount is not null && counted is not null ? counted - parsed.DeclaredAmount : null,
                RegisteredAt = parsed.RegisteredAt,
                CountedAt = bucket == DkCountingResultStatusMapper.Counted ? parsed.CountedAt : null,
                LastUpdatedAt = parsed.LastUpdatedAt
            }, cancellationToken);
            upserted++;
        }

        // Advanced to the end of the window we actually asked for, not to max(lastModified) in the
        // reply: an empty reply is normal on a two-minute poll and must still move time forward.
        await countingResults.RecordSyncRunAsync(now, now, rows.Count, upserted, skippedUnowned, cancellationToken);

        Telemetry = new Dictionary<string, string>
        {
            ["windowFromUtc"] = from.ToString("O"),
            ["windowToUtc"] = now.ToString("O"),
            ["rowsFetched"] = rows.Count.ToString(),
            ["rowsUpserted"] = upserted.ToString(),
            ["rowsSkippedUnowned"] = skippedUnowned.ToString(),
            ["rowsSkippedInvalid"] = skippedInvalid.ToString(),
            ["firstRun"] = (watermark is null).ToString()
        };

        // Nothing changed is the common case at this cadence, so it is logged at Debug to keep the
        // signal (a run that actually imported something) readable.
        if (rows.Count == 0)
        {
            logger.LogDebug("DK counting-result sync: no changes between {From:O} and {To:O}.", from, now);
            return;
        }

        logger.LogInformation(
            "DK counting-result sync complete. Window={From:O}..{To:O}, Fetched={Fetched}, Upserted={Upserted}, SkippedUnowned={SkippedUnowned}, SkippedInvalid={SkippedInvalid}",
            from, now, rows.Count, upserted, skippedUnowned, skippedInvalid);
    }

    // Field names as confirmed with ServiceNow. "regiteredDate" is their spelling on the wire
    // today; the corrected spelling is accepted too in case they fix it.
    private ParsedRow? TryParseRow(JsonElement row)
    {
        var serviceNowNumber = ReadString(row, "number") ?? ReadString(row, "sys_id");
        if (serviceNowNumber is null)
        {
            logger.LogWarning("Skipping a ServiceNow counting-result row with no 'number' or 'sys_id' field.");
            return null;
        }

        var lastUpdatedRaw = ReadString(row, "lastModified") ?? ReadString(row, "sys_updated_on");
        if (!clock.TryParse(lastUpdatedRaw, out var lastUpdatedAt))
        {
            logger.LogWarning(
                "Skipping ServiceNow row {ServiceNowNumber}: no parseable lastModified (raw={Raw}).",
                serviceNowNumber, lastUpdatedRaw ?? "(none)");
            return null;
        }

        return new ParsedRow(
            serviceNowNumber,
            ReadString(row, "clientID") ?? ReadString(row, "clientId"),
            ReadString(row, "customerRefNumber"),
            ReadString(row, "bagNumber"),
            ReadString(row, "state"),
            ReadDecimal(row, "registeredAmount"),
            ReadDecimal(row, "countedAmount"),
            clock.TryParse(ReadString(row, "regiteredDate") ?? ReadString(row, "registeredDate"), out var registeredAt) ? registeredAt : null,
            // countDate carries no time of day, so it must not be timezone-converted - see
            // ServiceNowClock.TryParseDateOrTimestamp.
            clock.TryParseDateOrTimestamp(ReadString(row, "countDate"), out var countedAt) ? countedAt : null,
            lastUpdatedAt);
    }

    private static string? ReadString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => NullIfEmpty(value.GetString()),
            JsonValueKind.Object when value.TryGetProperty("display_value", out var dv) && dv.ValueKind == JsonValueKind.String
                => NullIfEmpty(dv.GetString()),
            _ => null
        };
    }

    // Amounts arrive as JSON numbers (700.0) today and were strings before; accept both.
    private static decimal? ReadDecimal(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
        {
            return number;
        }

        return value.ValueKind == JsonValueKind.String &&
               decimal.TryParse(value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private sealed record ParsedRow(
        string ServiceNowNumber, string? ClientId, string? CustomerRefNumber, string? BagNumber, string? State,
        decimal? DeclaredAmount, decimal? CountedAmount, DateTime? RegisteredAt, DateTime? CountedAt, DateTime LastUpdatedAt);
}
