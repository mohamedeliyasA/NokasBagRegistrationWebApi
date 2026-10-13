using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;

namespace Nokas.CashMgmt.Dk.Api.Services;

public sealed class ContinuationTokenService : IContinuationTokenService
{
    private const string Purpose = "Nokas.CashMgmt.Dk.CountingResults.ContinuationToken.v1";

    private readonly IDataProtector _protector;

    public ContinuationTokenService(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(Purpose);
    }

    public string Wrap(string nativeToken, string queryHash)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(new TokenPayload(nativeToken, queryHash));
        var protectedBytes = _protector.Protect(payload);
        return WebEncoders.Base64UrlEncode(protectedBytes);
    }

    public ContinuationTokenPayload? TryUnwrap(string opaqueToken)
    {
        try
        {
            var protectedBytes = WebEncoders.Base64UrlDecode(opaqueToken);
            var payloadBytes = _protector.Unprotect(protectedBytes);
            var payload = JsonSerializer.Deserialize<TokenPayload>(payloadBytes);
            return payload is null ? null : new ContinuationTokenPayload(payload.NativeToken, payload.QueryHash);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException or JsonException)
        {
            return null;
        }
    }

    public string ComputeQueryHash(string clientId, DateTime fromDate, DateTime toDate, string dateType, string? status, string? customerRefNumber, int pageSize)
    {
        var canonical = string.Join('|',
            clientId,
            fromDate.ToString("O"),
            toDate.ToString("O"),
            dateType,
            status ?? "",
            customerRefNumber ?? "",
            pageSize);

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(hashBytes);
    }

    private sealed record TokenPayload(string NativeToken, string QueryHash);
}
