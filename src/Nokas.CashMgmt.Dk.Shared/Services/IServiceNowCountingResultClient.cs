using System.Text.Json;

namespace Nokas.CashMgmt.Dk.Shared.Services;

public interface IServiceNowCountingResultClient
{
    /// <summary>
    /// Every row ServiceNow has MODIFIED between the two instants (given in UTC; converted to
    /// ServiceNow's local time for the query). Returns null - never an empty list - when the call
    /// fails or the reply cannot be read, so a failure can never be mistaken for "nothing
    /// changed" and advance the watermark past data we never saw.
    /// </summary>
    Task<IReadOnlyList<JsonElement>?> FetchModifiedBetweenAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken);
}
