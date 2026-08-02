using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nokas.CashMgmt.BagRegistration.API.Configuration;

namespace Nokas.CashMgmt.BagRegistration.API.Services
{
    public interface IFunctionInvoker
    {
        Task<JsonElement?> CallFunctionAsync(string endpointName, string bagNumber, string correlationId, CancellationToken token);
    }

    public class FunctionInvoker : IFunctionInvoker
    {
        private readonly HttpClient _httpClient;
        private readonly BagStatusFunctionOptions _options;
        private readonly ILogger<FunctionInvoker> _logger;

        public FunctionInvoker(HttpClient httpClient, IOptions<BagStatusFunctionOptions> options, ILogger<FunctionInvoker> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<JsonElement?> CallFunctionAsync(string endpointName, string bagNumber, string correlationId, CancellationToken token)
        {
            var baseUrl = _options.BaseUrl.TrimEnd('/');
            var key = _options.FunctionKey;

            var url = $"{baseUrl}/api/{endpointName}/{Uri.EscapeDataString(bagNumber)}?code={Uri.EscapeDataString(key)}";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("x-correlation-id", correlationId);

            try
            {
                using var resp = await _httpClient.SendAsync(request, token);

                if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("Function {EndpointName} returned NotFound for bag {BagNumber}.", endpointName, bagNumber);
                    return null;
                }

                if (!resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync(token);
                    _logger.LogWarning("Function {EndpointName} returned non-success ({StatusCode}). Body: {Body}", endpointName, resp.StatusCode, body);
                    return null;
                }

                var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: token);
                return json;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Function {EndpointName} call cancelled or timed out for bag {BagNumber}", endpointName, bagNumber);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Azure Function {EndpointName} for bag {BagNumber}", endpointName, bagNumber);
                throw;
            }
        }
    }
}
