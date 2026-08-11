using System.Net;
using System.Web;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Nokas.CashMgmt.BagRegistration.Func.Services;

namespace Nokas.CashMgmt.BagRegistration.Func.Functions;

/// <summary>
/// Internal repair/backfill tool. Function-key auth, deliberately not exposed via APIM or
/// Swagger - only reachable directly against the Function App.
/// </summary>
public sealed class CountingResultResyncFunction(
    ICountingResultResyncService resyncService,
    ILogger<CountingResultResyncFunction> logger)
{
    [Function("CountingResultResync")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "resync/{customerNumber}")] HttpRequestData req,
        string customerNumber,
        FunctionContext context)
    {
        var correlationId = req.Headers.TryGetValues("x-correlation-id", out var values)
            ? values.FirstOrDefault() ?? Guid.NewGuid().ToString()
            : Guid.NewGuid().ToString();

        var query = HttpUtility.ParseQueryString(req.Url.Query);
        DateTime? fromDate = DateTime.TryParse(query["fromDate"], out var parsed) ? parsed.ToUniversalTime() : null;
        var purgeOtherClients = bool.TryParse(query["purgeOtherClients"], out var purge) && purge;

        logger.LogWarning(
            "CorrelationId: {CorrelationId} - Resync invoked for customer {CustomerNumber} (fromDate={FromDate}, purgeOtherClients={PurgeOtherClients}).",
            correlationId, customerNumber, fromDate, purgeOtherClients);

        try
        {
            var result = await resyncService.ResyncCustomerAsync(customerNumber, fromDate, purgeOtherClients, context.CancellationToken);

            if (!result.CustomerFound)
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteAsJsonAsync(new { message = "Customer number is not mapped.", customerNumber, correlationId, buildStamp = BuildInfo.Stamp });
                return notFound;
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                customerNumber,
                rowsUpserted = result.RowsUpserted,
                staleDocsPurged = result.StaleDocsPurged,
                correlationId,
                buildStamp = BuildInfo.Stamp
            });
            return response;
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "CorrelationId: {CorrelationId} - Resync failed for customer {CustomerNumber}", correlationId, customerNumber);

            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { message = "Resync failed.", correlationId, buildStamp = BuildInfo.Stamp });
            return errorResponse;
        }
    }
}
