using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Nokas.CashMgmt.BagReg.Func
{
    public class GetBagCountingResultFunction
    {
        private readonly IBagStatusService _bagStatusService;
        private readonly ILogger<GetBagCountingResultFunction> _logger;

        public GetBagCountingResultFunction(IBagStatusService bagStatusService, ILogger<GetBagCountingResultFunction> logger)
        {
            _bagStatusService = bagStatusService;
            _logger = logger;
        }

        [Function("GetBagCountingResult")]
        public async Task<HttpResponseData> RunAsync(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "countingresult/{bagNumber}")] HttpRequestData req,
            string bagNumber,
            FunctionContext context)
        {
            var correlationId = GetOrCreateCorrelationId(req);

            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId,
                ["BagNumber"] = bagNumber
            }))
            {
                _logger.LogInformation("Fetching counting result for Bag {BagNumber}", bagNumber);

                try
                {
                    var record = await _bagStatusService.GetCountingResultAsync(bagNumber, context.CancellationToken);

                    if (record == null)
                    {
                        _logger.LogWarning("No counting result found for Bag {BagNumber}", bagNumber);

                        var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                        await notFound.WriteAsJsonAsync(new
                        {
                            message = $"No counting result found for bag {bagNumber}",
                            correlationId
                        });
                        return notFound;
                    }

                    var responseModel = new
                    {
                        BagNumber = record.intInnkastnr,
                        CustomerNumber = record.vchCustomer_id,
                        DeclaredAmount = record.dblAmount ?? 0,
                        CountedAmount = record.dblControlledAmount ?? 0,
                        DeviationAmount = (record.dblControlledAmount ?? 0) - (record.dblAmount ?? 0),
                        StatusCode = record.intStatus,
                        Status = MapStatus(record.intStatus),
                        ControlledAt = record.dtmControlledAt,
                        DTMTransportFetch = record.dtmTransportFetch,
                        CorrelationId = correlationId
                    };

                    var ok = req.CreateResponse(HttpStatusCode.OK);
                    await ok.WriteAsJsonAsync(responseModel);
                    return ok;
                }
                catch (BagStatusSqlException sqlEx)
                {
                    _logger.LogError(sqlEx, "SQL error retrieving counting result for Bag {BagNumber}", bagNumber);

                    var svcErr = req.CreateResponse(HttpStatusCode.ServiceUnavailable);
                    await svcErr.WriteAsJsonAsync(new
                    {
                        message = "Database error occurred",
                        sqlEx.Server,
                        sqlEx.Database,
                        sqlEx.Number,
                        sqlEx.Procedure,
                        sqlEx.Message,
                        correlationId
                    });
                    return svcErr;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error retrieving counting result for Bag {BagNumber}", bagNumber);

                    var svcErr = req.CreateResponse(HttpStatusCode.InternalServerError);
                    await svcErr.WriteAsJsonAsync(new
                    {
                        message = "Unable to fetch counting result",
                        detail = ex.Message,
                        correlationId
                    });
                    return svcErr;
                }
            }
        }

        private static string GetOrCreateCorrelationId(HttpRequestData req)
        {
            return req.Headers.TryGetValues("x-correlation-id", out var vals)
                ? vals.FirstOrDefault() ?? Guid.NewGuid().ToString()
                : Guid.NewGuid().ToString();
        }

        private static string MapStatus(int? statusCode)
        {
            return statusCode switch
            {
                5 => "counted",
                3 => "picked-up",
                2 => "in-progress",
                1 => "registered",
                0 => "registered",
                4 => "received by bank",
                12 => "approved by bank",
                -1 or 8 or 9 => "error",
                10 => "canceled",
                11 => "not approved",
                13 => "missing",
                _ => "unknown"
            };
        }

    }
}
