using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Nokas.CashMgmt.No.Api.Configuration;
using Nokas.CashMgmt.No.Shared.Contracts;

namespace Nokas.CashMgmt.No.Api.Services;

public sealed class BagStatusFunctionClient(HttpClient httpClient, IOptions<BagStatusFunctionOptions> options) : IBagStatusFunctionClient
{
    public async Task<BagStatusRecord?> GetStatusAsync(string bagNumber, string correlationId, CancellationToken cancellationToken)
    {
        var config = options.Value;

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{config.BaseUrl}/api/status/{Uri.EscapeDataString(bagNumber)}?code={Uri.EscapeDataString(config.FunctionKey)}");
        request.Headers.Add("x-correlation-id", correlationId);

        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<BagStatusRecord>(cancellationToken);
    }
}
