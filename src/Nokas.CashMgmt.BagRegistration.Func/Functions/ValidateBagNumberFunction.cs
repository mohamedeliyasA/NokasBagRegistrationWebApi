using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Nokas.CashMgmt.BagRegistration.Func.Services;

namespace Nokas.CashMgmt.BagRegistration.Func.Functions;

public sealed class ValidateBagNumberFunction(IBagStatusService bagStatusService, ILogger<ValidateBagNumberFunction> logger)
{
    [Function("ValidateBagNumber")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "validate/{bagNumber}")] HttpRequestData req,
        string bagNumber,
        FunctionContext context)
    {
        var correlationId = req.Headers.TryGetValues("x-correlation-id", out var values)
            ? values.FirstOrDefault() ?? Guid.NewGuid().ToString()
            : Guid.NewGuid().ToString();

        try
        {
            var exists = await bagStatusService.ValidateBagNumberAsync(bagNumber, context.CancellationToken);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new { exists, correlationId });
            return response;
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "CorrelationId: {CorrelationId} - SQL error validating bag {BagNumber}", correlationId, bagNumber);

            var errorResponse = req.CreateResponse(HttpStatusCode.ServiceUnavailable);
            await errorResponse.WriteAsJsonAsync(new { message = "Database error occurred", correlationId });
            return errorResponse;
        }
    }
}