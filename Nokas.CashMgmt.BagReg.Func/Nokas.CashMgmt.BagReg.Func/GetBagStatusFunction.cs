using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Nokas.CashMgmt.BagReg.Func
{
    public class GetBagStatusFunction
    {
        private readonly IBagStatusService _bagStatusService;
        private readonly ILogger<GetBagStatusFunction> _logger;

        public GetBagStatusFunction(IBagStatusService bagStatusService, ILogger<GetBagStatusFunction> logger)
        {
            _bagStatusService = bagStatusService;
            _logger = logger;
        }

        [Function("GetBagStatus")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "status/{bagNumber}")]
             HttpRequestData req, string bagNumber, FunctionContext context)
        {
            var correlationId = req.Headers.TryGetValues("x-correlation-id", out var vals)
                ? vals.FirstOrDefault()
                : Guid.NewGuid().ToString();

            _logger.LogInformation("Checking bag {Bag} Correlation {Corr}", bagNumber, correlationId);

            try
            {
                var record = await _bagStatusService.GetStatusAsync(bagNumber, context.CancellationToken);

                if (record == null)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteAsJsonAsync(new { message = "Bag not found" });
                    return notFound;
                }

                var ok = req.CreateResponse(HttpStatusCode.OK);
                await ok.WriteAsJsonAsync(record);
                return ok;
            }
            catch (BagStatusSqlException sqlEx)
            {
                _logger.LogError(sqlEx, "SQL Error checking bag status for {Bag}", bagNumber);

                var svcErr = req.CreateResponse(HttpStatusCode.ServiceUnavailable);
                await svcErr.WriteAsJsonAsync(new
                {
                    message = "Database error occurred",
                    server = sqlEx.Server,
                    database = sqlEx.Database,
                    number = sqlEx.Number,
                    procedure = sqlEx.Procedure,
                    errorMessage = sqlEx.Message
                });
                return svcErr;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking bag status for {Bag}", bagNumber);
                var svcErr = req.CreateResponse(HttpStatusCode.ServiceUnavailable);
                await svcErr.WriteAsJsonAsync(new { message = "Unable to verify bag status", detail = ex.Message });
                return svcErr;
            }
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
