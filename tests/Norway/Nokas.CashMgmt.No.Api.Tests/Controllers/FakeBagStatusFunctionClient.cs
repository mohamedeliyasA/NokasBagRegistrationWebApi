using Nokas.CashMgmt.No.Api.Services;
using Nokas.CashMgmt.No.Shared.Contracts;

namespace Nokas.CashMgmt.No.Api.Tests.Controllers;

public sealed class FakeBagStatusFunctionClient : IBagStatusFunctionClient
{
    public BagStatusRecord? Result { get; set; }
    public Exception? ExceptionToThrow { get; set; }

    public Task<BagStatusRecord?> GetStatusAsync(string bagNumber, string correlationId, CancellationToken cancellationToken)
    {
        if (ExceptionToThrow is not null)
        {
            throw ExceptionToThrow;
        }

        return Task.FromResult(Result);
    }
}
