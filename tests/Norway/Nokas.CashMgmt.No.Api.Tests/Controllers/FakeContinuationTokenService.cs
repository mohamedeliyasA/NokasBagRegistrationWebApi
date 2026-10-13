using Nokas.CashMgmt.No.Api.Services;

namespace Nokas.CashMgmt.No.Api.Tests.Controllers;

public sealed class FakeContinuationTokenService : IContinuationTokenService
{
    public ContinuationTokenPayload? UnwrapResult { get; set; }
    public string WrapReturnValue { get; set; } = "wrapped-token";

    public string Wrap(string nativeToken, string queryHash) => WrapReturnValue;

    public ContinuationTokenPayload? TryUnwrap(string opaqueToken) => UnwrapResult;

    public string ComputeQueryHash(string clientId, DateTime fromDate, DateTime toDate, string dateType, string? status, string? customerRefNumber, int pageSize) =>
        "fake-hash";
}
