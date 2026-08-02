
using System.Text.Json;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nokas.CashMgmt.BagRegistration.API.Helpers;
using Nokas.CashMgmt.BagRegistration.API.Models;
using Nokas.CashMgmt.BagRegistration.API.Repository;
using Nokas.CashMgmt.BagRegistration.API.Services;

namespace Nokas.CashMgmt.BagRegistration.API.Controllers
{
    [Authorize(Policy = "PartnerAccess")]
    [Route("api/cash-mgmt/bag-registration")]
    [ApiController]
    [Produces("application/json", "application/xml")]
    public class BagRegistrationController : ControllerBase
    {
        private readonly ILogger<BagRegistrationController> _logger;
        private readonly IBagRegistrationRepo _bagRegistrationRepo;
        private readonly IMapper _mapper;
        private readonly IFunctionInvoker _functionInvoker;

        public BagRegistrationController(
            ILogger<BagRegistrationController> logger,
            IBagRegistrationRepo bagRegistrationRepo,
            IMapper mapper,
            IFunctionInvoker functionInvoker)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _bagRegistrationRepo = bagRegistrationRepo ?? throw new ArgumentNullException(nameof(bagRegistrationRepo));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _functionInvoker = functionInvoker ?? throw new ArgumentNullException(nameof(functionInvoker));
        }

        private string GetCorrelationId() =>
          HttpContext?.Items["CorrelationId"]?.ToString() ?? "N/A";

        [HttpGet("{bagNumber}", Name = "GetBagRegistration")]
        [ProducesResponseType(typeof(BagRegistrationRequestForCreation), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(BagRegResponse), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<BagRegistrationRequestForCreation>> GetBagRegistrationByNumber(string bagNumber)
        {
            var requestCorrelationId = GetCorrelationId();
            var clientId = ClaimsHelper.GetClientId(User);

            if (string.IsNullOrEmpty(clientId))
            {
                _logger.LogWarning("RequestCorrelationId: {CorrelationId} - Missing clientId.", requestCorrelationId);
                return Unauthorized();
            }

            var bagRegistration = await _bagRegistrationRepo.GetBagByNumberForClientAsync(bagNumber, clientId, HttpContext.RequestAborted);
            if (bagRegistration == null)
            {
                _logger.LogWarning("BagNotFound, Bag Number: {bagNumber} CorrelationId: {CorrelationId} - ClientId : {clientId}", bagNumber, requestCorrelationId, clientId);
                return NotFound(BagRegResponse.CreateNotFoundSummary(bagNumber, clientId, requestCorrelationId));
            }

            var bagRegRes = _mapper.Map<BagRegistrationRequestForCreation>(bagRegistration);

            return Ok(bagRegRes);
        }


        [HttpPost]
        [ProducesResponseType(typeof(BagRegResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(BagRegResponse), StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<ActionResult<BagRegResponse>> CreateBagRegistration(BagRegistrationRequestForCreation bagRequest)
        {
            var bagLifecycleId = Guid.NewGuid().ToString();
            var requestCorrelationId = GetCorrelationId();
            var clientId = ClaimsHelper.GetClientId(User);

            // Validate if bag exist before creation.
            try
            {
                var json = await _functionInvoker.CallFunctionAsync("validate", bagRequest.BagRegistration.BagNumber, requestCorrelationId, HttpContext.RequestAborted);
                if (json.HasValue && json.Value.TryGetProperty("exists", out var existsProp) && existsProp.GetBoolean())
                {
                    _logger.LogWarning("Bag already exists in system: {BagNumber}", bagRequest.BagRegistration.BagNumber);
                    var existingResponse = new BagRegResponse
                    {
                        customerRefNumber = bagRequest.BagRegistration.CustomerRefNumber,
                        BagNumber = bagRequest.BagRegistration.BagNumber,
                        RegistrationStatus = "AlreadyExists",
                        Description = $"Bag {bagRequest.BagRegistration.BagNumber} already exists and cannot be re-registered.",
                        BagLifecycleId = bagLifecycleId,
                        RequestCorrelationId = requestCorrelationId
                    };
                    return Conflict(existingResponse);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating bag number {BagNumber}", bagRequest.BagRegistration.BagNumber);
                return StatusCode(StatusCodes.Status503ServiceUnavailable, "Unable to validate bag number.");
            }


            // Optionally log blobName captured by middleware
            var blobName = HttpContext.Items["BlobName"]?.ToString();
            if (!string.IsNullOrEmpty(blobName))
            {
                _logger.LogInformation("Payload queued for archiving. BlobName: {BlobName}", blobName);
            }

            // Map DTO and enrich metadata
            var newBagRegistrationRequest = _mapper.Map<BagRegistrationRequest>(bagRequest);
            newBagRegistrationRequest.ClientId = clientId;
            newBagRegistrationRequest.BagLifecycleId = bagLifecycleId;
            newBagRegistrationRequest.CorrelationId = requestCorrelationId;
            newBagRegistrationRequest.BagRegistration.RegisteredBy = $@"API_{clientId}";

            newBagRegistrationRequest.OperationHistory.Add(new OperationHistoryEntry
            {
                Action = "Create",
                Timestamp = DateTime.UtcNow,
                BagLifeCycleId = bagLifecycleId,
                RequestCorrelationId = requestCorrelationId,
                PerformedBy = clientId,
                archiveBlobName = HttpContext.Items["ArchivedBlobName"] as string
            });

            bagRequest.BagRegistration.ActionFlag = "Create";
            bagRequest.BagRegistration.RegisteredBy = $@"API_{clientId}";

            var outboxMessage = new OutboxMessage
            {
                Subject = "bagregistration",
                Payload = JsonSerializer.Serialize(bagRequest)
            };

            // Atomically save the bag document and its outbox event (Cosmos transactional batch) —
            // Cosmos is the single source of truth; the outbox dispatcher publishes to Service Bus.
            await _bagRegistrationRepo.CreateBagRegistration(newBagRegistrationRequest, outboxMessage, HttpContext.RequestAborted);

            var bagSummary = new BagRegResponse
            {
                customerRefNumber = newBagRegistrationRequest.BagRegistration.CustomerRefNumber,
                BagNumber = newBagRegistrationRequest.BagRegistration.BagNumber,
                RegistrationStatus = newBagRegistrationRequest.RegistrationStatus,
                Description = "Bag Registered Successfully",
                BagLifecycleId = bagLifecycleId,
                RequestCorrelationId = requestCorrelationId
            };

            _logger.LogInformation("BagLifecycleId: {BagLifecycleId}, CorrelationId: {CorrelationId} - Bag registration created successfully.",
                bagLifecycleId, requestCorrelationId);

            return CreatedAtRoute("GetBagRegistration", new { bagNumber = bagSummary.BagNumber }, bagSummary);
        }

        [HttpDelete("{bagNumber}", Name = "DeleteBag")]
        [ProducesResponseType(typeof(BagRegResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BagRegResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(BagRegResponse), StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status408RequestTimeout)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<ActionResult<BagRegResponse>> DeleteBagRegistration(string bagNumber)
        {
            // Extract all expected properties
            int? statusValue = null;
            string? intInnkastnr = null;
            string? vchName = null;
            string? vchCustomer_id = null;
            string? dblAmount = null;
            string? vchLocation = null;
            DateTime? dtmRegDate = null;
            DateTime? dtmTransportFetch = null;

            var requestCorrelationId = GetCorrelationId();
            var clientId = ClaimsHelper.GetClientId(User);

            var existing = await _bagRegistrationRepo.GetBagByNumberForClientAsync(bagNumber, clientId, HttpContext.RequestAborted);
            if (existing == null)
                return NotFound(BagRegResponse.CreateNotFoundSummary(bagNumber, clientId, requestCorrelationId));

            try
            {
                // ✅ Call Azure Function using the shared helper
                var jsonElement = await _functionInvoker.CallFunctionAsync("status", bagNumber, requestCorrelationId, HttpContext.RequestAborted);

                if (jsonElement == null)
                {
                    _logger.LogWarning("Bag not found in status function for bag {Bag}.", bagNumber);
                    return NotFound(new { message = "Bag not found (status verification)." });
                }

                var json = jsonElement.Value;

                // --- Parse JSON values ---
                if (json.TryGetProperty("intStatus", out var st) && st.TryGetInt32(out var sVal))
                    statusValue = sVal;

                if (json.TryGetProperty("intInnkastnr", out var inVal))
                    intInnkastnr = inVal.GetString();

                if (json.TryGetProperty("vchCustomer_id", out var custProp))
                    vchCustomer_id = custProp.GetString();

                if (json.TryGetProperty("vchName", out var custName))
                    vchName = custName.GetString();

                if (json.TryGetProperty("dblAmount", out var DblAmount))
                    dblAmount = DblAmount.GetString();

                if (json.TryGetProperty("vchLocation", out var VchLocation))
                    vchLocation = VchLocation.GetString();

                if (json.TryGetProperty("dtmRegDate", out var dtProp) &&
                    DateTime.TryParse(dtProp.GetString(), out var parsedDt))
                    dtmRegDate = parsedDt;

                if (json.TryGetProperty("dtmTransportFetch", out var dtTransFetch) &&
                    DateTime.TryParse(dtTransFetch.GetString(), out var parseDtTransFetch))
                    dtmTransportFetch = parseDtTransFetch;

                if (statusValue == null)
                {
                    _logger.LogWarning("Missing status in function response for bag {Bag}.", bagNumber);
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Invalid status response.");
                }

                string mappedStatus = MapStatus(statusValue, dtmTransportFetch);
                existing.RegistrationStatus = mappedStatus;

                // --- Handle already cancelled case ---
                if (statusValue == 10)
                {
                    if (existing.RegistrationStatus != "Deleted")
                    {
                        existing.RegistrationStatus = "Deleted";
                        existing.BagRegistration.ActionFlag = "Cancelled";
                        await _bagRegistrationRepo.UpdateBagRegistration(existing, HttpContext.RequestAborted);
                    }

                    var res = _mapper.Map<BagRegResponse>(existing);
                    res.Description = "Bag already Cancelled";
                    res.RequestCorrelationId = requestCorrelationId;
                    res.BagLifecycleId = existing.BagLifecycleId;
                    return Conflict(res);
                }

                // Bag can be cancelled only if statusValue is 0, 1, or 2 and no transport fetch yet.
                bool isValidStatus = statusValue == 0 || statusValue == 1 || statusValue == 2;

                if (!isValidStatus || dtmTransportFetch.HasValue)
                {
                    var res = _mapper.Map<BagRegResponse>(existing);
                    res.Description = "Bag already processed and cannot be cancelled at this stage.";
                    res.RequestCorrelationId = requestCorrelationId;
                    res.BagLifecycleId = existing.BagLifecycleId;
                    return BadRequest(res);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Bag status verification cancelled for bag {Bag}", bagNumber);
                return StatusCode(StatusCodes.Status408RequestTimeout, "Bag status verification timed out.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying bag status for bag {Bag}", bagNumber);
                return StatusCode(StatusCodes.Status503ServiceUnavailable, "Unable to verify bag status.");
            }

            // --- Proceed with deletion ---
            existing.RegistrationStatus = "Deleted";
            existing.BagRegistration.ActionFlag = "Cancelled";

            existing.OperationHistory.Add(new OperationHistoryEntry
            {
                Action = "Delete",
                Timestamp = DateTime.UtcNow,
                BagLifeCycleId = existing.CorrelationId,
                RequestCorrelationId = requestCorrelationId,
                PerformedBy = clientId
            });

            // --- Build cancellation message ---
            var cancelMessage = new
            {
                cancelBagRegistration = new
                {
                    customerRefNumber = existing.BagRegistration.CustomerRefNumber,
                    bagNumber = existing.BagRegistration.BagNumber,
                    actionFlag = "cancel",
                    registeredBy = $"API_{clientId}",
                    cancellationDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                    intInnkastnr,
                    vchCustomer_id,
                    vchName,
                    dblAmount,
                    vchLocation,
                    dtmRegDate,
                    intStatus = statusValue
                }
            };

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };

            var outboxMessage = new OutboxMessage
            {
                Subject = "deleteBagRegistration",
                Payload = JsonSerializer.Serialize(cancelMessage, jsonOptions)
            };

            // Atomically update the bag document and write its outbox event (Cosmos transactional batch).
            await _bagRegistrationRepo.UpdateBagRegistrationWithOutboxAsync(existing, outboxMessage, HttpContext.RequestAborted);

            var bagSummaryRes = new BagRegResponse
            {
                customerRefNumber = existing.BagRegistration.CustomerRefNumber,
                BagNumber = existing.BagRegistration.BagNumber,
                RegistrationStatus = existing.RegistrationStatus,
                Description = "Bag Cancelled Successfully.",
                BagLifecycleId = existing.BagLifecycleId,
                RequestCorrelationId = requestCorrelationId
            };

            return Ok(bagSummaryRes);
        }

        private static string MapStatus(int? statusCode, DateTime? dtmTransportFetch = null)
        {
            if (dtmTransportFetch.HasValue && (statusCode == 0 || statusCode == 1 || statusCode == 2))
                return "picked-up";

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
