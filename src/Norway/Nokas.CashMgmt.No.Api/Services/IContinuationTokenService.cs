namespace Nokas.CashMgmt.No.Api.Services;

public sealed record ContinuationTokenPayload(string NativeToken, string QueryHash);

/// <summary>
/// Wraps/unwraps the native Cosmos continuation token so partners never see or construct one
/// directly. Embeds a hash of the query parameters that produced the token so a caller can't
/// reuse a token against a different query on the next call.
/// </summary>
public interface IContinuationTokenService
{
    string Wrap(string nativeToken, string queryHash);

    /// <returns>Null if the token is malformed or fails Data Protection unprotect (tampered/expired/wrong key) - never throws for caller input.</returns>
    ContinuationTokenPayload? TryUnwrap(string opaqueToken);

    string ComputeQueryHash(string clientId, DateTime fromDate, DateTime toDate, string dateType, string? status, string? customerRefNumber, int pageSize);
}
