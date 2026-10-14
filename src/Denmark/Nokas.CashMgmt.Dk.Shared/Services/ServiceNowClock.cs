using System.Globalization;

using Microsoft.Extensions.Logging;

namespace Nokas.CashMgmt.Dk.Shared.Services;

/// <summary>
/// Translates between UTC and the wall-clock time ServiceNow speaks.
///
/// Their API sends and expects "yyyy-MM-dd HH:mm:ss" with no offset, in Central European time
/// (CET/CEST — the offset shifts with summer time; a bag registered 06:14 UTC in September came
/// back as 08:14, i.e. UTC+2). Everything we store stays UTC, so the conversion happens exactly
/// twice: on the window we ask for, and on the timestamps we read back.
///
/// Getting this wrong is silent rather than loud: with a two-minute window, an offset of an hour
/// means every call returns nothing and the sync looks healthy while never importing anything.
/// </summary>
public sealed class ServiceNowClock
{
    public const string Format = "yyyy-MM-dd HH:mm:ss";

    private readonly TimeZoneInfo _zone;

    public ServiceNowClock(string timeZoneId, ILogger? logger = null)
    {
        _zone = Resolve(timeZoneId, logger);
    }

    public string ZoneId => _zone.Id;

    /// <summary>Formats a UTC instant as the local wall-clock string ServiceNow expects in a query.</summary>
    public string ToQueryString(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _zone)
            .ToString(Format, CultureInfo.InvariantCulture);

    /// <summary>
    /// Reads one of their timestamps as UTC. Accepts the date-only form they use for countDate.
    /// An ambiguous local time (the hour that repeats when summer time ends) resolves to standard
    /// time, and an invalid one (the hour that does not exist in spring) is nudged forward - both
    /// are one-hour effects on a record's own timestamp, never on the query window.
    /// </summary>
    /// <summary>
    /// Parses a value that may be a bare date ("yyyy-MM-dd") or a full timestamp.
    /// <para>
    /// A bare date is a label for a calendar day, not an instant, so it is returned as midnight UTC
    /// of that same day and never passed through the timezone conversion. Converting it would move
    /// it: midnight CEST in UTC is 22:00 the previous day, which pushed every counted date back one
    /// day and made a dateType=counted query return the wrong day's bags.
    /// </para>
    /// <para>
    /// A full timestamp still goes through <see cref="TryParse"/>, because that genuinely is an
    /// instant expressed in their wall clock and does need converting.
    /// </para>
    /// </summary>
    public bool TryParseDateOrTimestamp(string? raw, out DateTime utc)
    {
        utc = default;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var trimmed = raw.Trim();

        if (DateTime.TryParseExact(trimmed, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var calendarDate))
        {
            utc = DateTime.SpecifyKind(calendarDate.Date, DateTimeKind.Utc);
            return true;
        }

        return TryParse(trimmed, out utc);
    }

    public bool TryParse(string? raw, out DateTime utc)
    {
        utc = default;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        if (!DateTime.TryParseExact(raw.Trim(), [Format, "yyyy-MM-dd"], CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var local)
            && !DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out local))
        {
            return false;
        }

        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);

        if (_zone.IsInvalidTime(local))
        {
            local = local.AddHours(1);
        }

        utc = TimeZoneInfo.ConvertTimeToUtc(local, _zone);
        return true;
    }

    // .NET accepts Windows ids on Windows and IANA ids everywhere; try the configured value, then
    // the other spelling, and only then fall back to UTC - loudly, because a silent fallback here
    // would shift every timestamp by an hour or two without anything looking broken.
    private static TimeZoneInfo Resolve(string timeZoneId, ILogger? logger)
    {
        foreach (var candidate in new[] { timeZoneId, "Central European Standard Time", "Europe/Copenhagen" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(candidate);
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
            }
        }

        logger?.LogCritical(
            "Time zone '{TimeZoneId}' could not be resolved. Falling back to UTC - ServiceNow timestamps will be off by the CET offset.",
            timeZoneId);
        return TimeZoneInfo.Utc;
    }
}
