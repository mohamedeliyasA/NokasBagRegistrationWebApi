using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Nokas.CashMgmt.No.Func.Services;

namespace Nokas.CashMgmt.No.Func.Functions;

public sealed class GetBagStatusFunction(IBagStatusService bagStatusService, ILogger<GetBagStatusFunction> logger)
{
    [Function("GetBagStatus")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "status/{bagNumber}")] HttpRequestData req,
        string bagNumber,
        FunctionContext context)
    {
        var correlationId = req.Headers.TryGetValues("x-correlation-id", out var values)
            ? values.FirstOrDefault() ?? Guid.NewGuid().ToString()
            : Guid.NewGuid().ToString();

        try
        {
            var record = await bagStatusService.GetStatusAsync(bagNumber, context.CancellationToken);

            if (record is null)
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteAsJsonAsync(new { message = "Bag not found", correlationId, buildStamp = BuildInfo.Stamp });
                return notFound;
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(record);
            return response;
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "CorrelationId: {CorrelationId} - SQL error checking status for bag {BagNumber}", correlationId, bagNumber);

            var errorResponse = req.CreateResponse(HttpStatusCode.ServiceUnavailable);
            await errorResponse.WriteAsJsonAsync(new { message = "Database error occurred", correlationId, buildStamp = BuildInfo.Stamp });
            return errorResponse;
        }
    }
}
