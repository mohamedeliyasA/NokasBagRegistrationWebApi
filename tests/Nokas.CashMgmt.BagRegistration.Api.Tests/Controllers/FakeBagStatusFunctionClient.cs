using Nokas.CashMgmt.BagRegistration.Api.Services;
using Nokas.CashMgmt.Shared.Contracts;

namespace Nokas.CashMgmt.BagRegistration.Api.Tests.Controllers;

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
