using Nokas.CashMgmt.No.Shared.Contracts;

namespace Nokas.CashMgmt.No.Api.Services;

public interface IBagStatusFunctionClient
{
    Task<BagStatusRecord?> GetStatusAsync(string bagNumber, string correlationId, CancellationToken cancellationToken);
}
