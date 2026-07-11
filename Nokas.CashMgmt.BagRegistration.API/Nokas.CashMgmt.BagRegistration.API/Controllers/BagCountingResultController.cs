using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nokas.CashMgmt.BagRegistration.API.Helpers;
using Nokas.CashMgmt.BagRegistration.API.Models;
using Nokas.CashMgmt.BagRegistration.API.Repository;
using Nokas.CashMgmt.BagRegistration.API.Services;
using System.Text.Json;

namespace Nokas.CashMgmt.BagRegistration.API.Controllers
{
    [Authorize]
    [Route("api/cash-mgmt/bag-countingresult")]
    [ApiController]
    [Produces("application/json", "application/xml")]
    public class BagCountingResultController : Controller
    {
        private readonly ILogger<BagCountingResultController> _logger;
        private readonly IBagRegistrationRepo _bagRegistrationRepo;
        private readonly IBlobArchiveService _blobArchiveService;
        private readonly IServiceBusSender _serviceBusSender;
        private readonly IMapper _mapper;
        private readonly IConfiguration _configuration;
        private readonly IFunctionInvoker _functionInvoker;

        public BagCountingResultController(
            ILogger<BagCountingResultController> logger,
            IBagRegistrationRepo bagRegistrationRepo,
            IBlobArchiveService blobArchiveService,
            IServiceBusSender serviceBusSender,
            IConfiguration configuration,
            IMapper mapper,
            IFunctionInvoker functionInvoker)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _bagRegistrationRepo = bagRegistrationRepo ?? throw new ArgumentNullException(nameof(bagRegistrationRepo));
            _blobArchiveService = blobArchiveService ?? throw new ArgumentNullException(nameof(blobArchiveService));
            _serviceBusSender = serviceBusSender ?? throw new ArgumentNullException(nameof(serviceBusSender));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _functionInvoker = functionInvoker ?? throw new ArgumentNullException(nameof(functionInvoker));
        }

        private string GetCorrelationId() =>
            HttpContext?.Items["CorrelationId"]?.ToString() ?? Guid.NewGuid().ToString();

        [HttpGet("{bagNumber}")]
        public async Task<IActionResult> GetBagCountingResult(string bagNumber)
        {
            string? customerNumber = null;
            decimal? declaredAmount = null;
            decimal? countedAmount = null;
            decimal? deviationAmount = null;
            string? status = null;
            string? countedDate = null;
            int? statusCode = null;
            DateTime? dtmTransportFetch = null;
            string? bagStatus = null;

            var requestCorrelationId = GetCorrelationId();
            var clientId = ClaimsHelper.GetClientId(User);

            var existing = await _bagRegistrationRepo.GetBagByNumberForClientAsync(bagNumber, clientId);
            if (existing == null)
                return NotFound(BagRegResponse.CreateNotFoundSummary(bagNumber, clientId, requestCorrelationId));

            try
            {
                // ✅ Call counting result Azure Function
                var jsonElement = await _functionInvoker.CallFunctionAsync("countingresult", bagNumber, requestCorrelationId, HttpContext.RequestAborted);

                if (jsonElement == null)
                    return NotFound(new { message = "Bag not found (counting result)." });

                var json = jsonElement.Value;
                customerNumber = existing.BagRegistration.CustomerRefNumber;

                // --- Parse properties ---
                if (json.TryGetProperty("DeclaredAmount", out var da) && da.TryGetDecimal(out var daVal))
                    declaredAmount = daVal;

                if (json.TryGetProperty("CountedAmount", out var ca) && ca.TryGetDecimal(out var caVal))
                    countedAmount = caVal;

                if (json.TryGetProperty("DeviationAmount", out var dv) && dv.TryGetDecimal(out var dvVal))
                    deviationAmount = dvVal;

                if (json.TryGetProperty("Status", out var statusProp))
                    status = statusProp.GetString();

                if (json.TryGetProperty("ControlledAt", out var caDateEl))
                {
                    if (caDateEl.ValueKind == JsonValueKind.String)
                        countedDate = caDateEl.GetString();
                    else
                        countedDate = caDateEl.GetRawText();
                }
                if (json.TryGetProperty("StatusCode", out var st) && st.TryGetInt32(out var stVal))
                    statusCode = stVal;

                if (json.TryGetProperty("DTMTransportFetch", out var dtTransFetch) &&
                   DateTime.TryParse(dtTransFetch.GetString(), out var parseDtTransFetch))
                    dtmTransportFetch = parseDtTransFetch;

             
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Bag counting result verification cancelled for bag {Bag}", bagNumber);
                return StatusCode(StatusCodes.Status408RequestTimeout, "Bag counting verification timed out.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying bag counting result for bag {Bag}", bagNumber);
                return StatusCode(StatusCodes.Status503ServiceUnavailable, "Unable to verify bag counting result.");
            }

            // ✅ Add history
            existing.OperationHistory.Add(new OperationHistoryEntry
            {
                Action = "BagCountingResultRetrieved",
                Timestamp = DateTime.UtcNow,
                BagLifeCycleId = existing.CorrelationId,
                RequestCorrelationId = requestCorrelationId,
                PerformedBy = clientId
            });

            // ✅ Build response dynamically

            if (dtmTransportFetch.HasValue && (statusCode == 0 || statusCode == 1 || statusCode == 2))
                bagStatus = "picked-up";
            else
            {
                bagStatus = status ?? "unknown";
            }


            BagCountingResultResponse response;
            if (statusCode == 5) // counted
            {
                response = new BagCountingResultResponse
                {
                    customerRefNumber = customerNumber,
                    BagNumber = bagNumber,
                    Description = "Bag counting result retrieved successfully.",
                    BagStatus = bagStatus,
                    BagLifecycleId = existing.BagLifecycleId,
                    RequestCorrelationId = requestCorrelationId,
                    DeclaredAmount = declaredAmount ?? 0m,
                    ActualCountedAmount = countedAmount ?? 0m,
                    DeviationAmount = deviationAmount ?? 0m,
                    CountedDate = countedDate
                };
            }
            else if (statusCode == 10) // cancelled
            {
                response = new BagCountingResultResponse
                {
                    customerRefNumber = customerNumber,
                    BagNumber = bagNumber,
                    Description = "Bag in cancelled state",
                    BagStatus = bagStatus,
                    BagLifecycleId = existing.BagLifecycleId,
                    RequestCorrelationId = requestCorrelationId,
                    DeclaredAmount = declaredAmount ?? 0m
                };
            }
            else // not yet counted
            {
                response = new BagCountingResultResponse
                {
                    customerRefNumber = customerNumber,
                    BagNumber = bagNumber,
                    Description = "Bag not yet counted.",
                    BagStatus = bagStatus,
                    BagLifecycleId = existing.BagLifecycleId,
                    RequestCorrelationId = requestCorrelationId,
                    DeclaredAmount = declaredAmount ?? 0m
                };
            }

            return Ok(response);
        }
    }
}
