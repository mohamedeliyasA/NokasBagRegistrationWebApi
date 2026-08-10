namespace Nokas.CashMgmt.BagRegistration.Func.Services;

public interface IBagStatusService
{
    Task<bool> ValidateBagNumberAsync(string bagNumber, CancellationToken cancellationToken);

    Task<IReadOnlyList<(string BagNumber, DateTime CreatedAt)>> GetBagNumbersCreatedAfterAsync(
        DateTime sinceUtc, int maxRows, CancellationToken cancellationToken);
}