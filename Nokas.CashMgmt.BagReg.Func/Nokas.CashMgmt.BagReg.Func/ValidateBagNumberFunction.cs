using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Nokas.CashMgmt.BagReg.Func
{
    public class ValidateBagNumberFunction
    {
        private readonly IBagStatusService _bagStatusService;
        private readonly ILogger<ValidateBagNumberFunction> _logger;

        public ValidateBagNumberFunction(IBagStatusService bagStatusService, ILogger<ValidateBagNumberFunction> logger)
        {
            _bagStatusService = bagStatusService;
            _logger = logger;
        }

        [Function("ValidateBagNumber")]
        public async Task<HttpResponseData> RunAsync(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "validate/{bagNumber}")] HttpRequestData req,
            string bagNumber,
            FunctionContext context)
        {
            var correlationId = req.Headers.TryGetValues("x-correlation-id", out var vals)
                ? vals.FirstOrDefault() ?? Guid.NewGuid().ToString()
                : Guid.NewGuid().ToString();

            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId,
                ["BagNumber"] = bagNumber
            }))
            {
                _logger.LogInformation("Validating existence of BagNumber {BagNumber}", bagNumber);

                try
                {
                    var exists = await _bagStatusService.ValidateBagNumber(bagNumber, context.CancellationToken);

                    var response = req.CreateResponse(HttpStatusCode.OK);
                    await response.WriteAsJsonAsync(new
                    {
                        bagNumber,
                        exists,
                        correlationId
                    });
                    return response;
                }
                catch (BagStatusSqlException sqlEx)
                {
                    _logger.LogError(sqlEx, "SQL error validating BagNumber {BagNumber}", bagNumber);

                    var svcErr = req.CreateResponse(HttpStatusCode.ServiceUnavailable);
                    await svcErr.WriteAsJsonAsync(new
                    {
                        message = "Database error occurred during validation",
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
                    _logger.LogError(ex, "Unexpected error validating BagNumber {BagNumber}", bagNumber);

                    var svcErr = req.CreateResponse(HttpStatusCode.InternalServerError);
                    await svcErr.WriteAsJsonAsync(new
                    {
                        message = "Unable to validate bag number",
                        detail = ex.Message,
                        correlationId
                    });
                    return svcErr;
                }
            }
        }
    }
}
