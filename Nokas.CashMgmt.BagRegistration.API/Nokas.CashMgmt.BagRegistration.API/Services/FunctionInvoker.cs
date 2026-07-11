using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Nokas.CashMgmt.BagRegistration.API.Services
{
    public interface IFunctionInvoker
    {
        Task<JsonElement?> CallFunctionAsync(string endpointName, string bagNumber, string correlationId, CancellationToken token);
    }

    public class FunctionInvoker : IFunctionInvoker
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<FunctionInvoker> _logger;

        public FunctionInvoker(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<FunctionInvoker> logger)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<JsonElement?> CallFunctionAsync(string endpointName, string bagNumber, string correlationId, CancellationToken token)
        {
            var baseUrl = _configuration["BagStatusFunction:BaseUrl"]?.TrimEnd('/');
            var key = _configuration["BagStatusFunction:FunctionKey"];

            if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(key))
            {
                _logger.LogWarning("Function base URL or key is not configured.");
                throw new InvalidOperationException("Azure Function configuration missing.");
            }

            var url = $"{baseUrl}/api/{endpointName}/{Uri.EscapeDataString(bagNumber)}?code={Uri.EscapeDataString(key)}";

            var httpClient = _httpClientFactory.CreateClient();
            if (!httpClient.DefaultRequestHeaders.Contains("x-correlation-id"))
                httpClient.DefaultRequestHeaders.Add("x-correlation-id", correlationId);

            try
            {
                using var resp = await httpClient.GetAsync(url, token);

                if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("Function {EndpointName} returned NotFound for bag {BagNumber}.", endpointName, bagNumber);
                    return null;
                }

                if (!resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync();
                    _logger.LogWarning("Function {EndpointName} returned non-success ({StatusCode}). Body: {Body}", endpointName, resp.StatusCode, body);
                    return null;
                }

                var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: token);
                return json;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Function {EndpointName} call cancelled for bag {BagNumber}", endpointName, bagNumber);
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
