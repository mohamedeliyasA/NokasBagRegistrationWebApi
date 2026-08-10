using Nokas.CashMgmt.Shared.Contracts;

namespace Nokas.CashMgmt.BagRegistration.Api.Services;

public interface IBagStatusFunctionClient
{
    Task<BagStatusRecord?> GetStatusAsync(string bagNumber, string correlationId, CancellationToken cancellationToken);
}
