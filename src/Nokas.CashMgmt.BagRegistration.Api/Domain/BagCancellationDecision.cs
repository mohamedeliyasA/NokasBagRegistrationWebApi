namespace Nokas.CashMgmt.BagRegistration.Api.Domain;

public enum BagCancellationOutcome
{
    Cancelled,
    AlreadyCancelled,
    NotCancellable
}

// dbo.api_CancelBagIfRegistered performs the cancellation itself and returns the pre-cancel
// row; this reconstructs the same decision purely from that row's intStatus/dtmTransportFetch,
// since the proc never distinguishes "already cancelled" from "not cancellable" in its output.
public static class BagCancellationDecision
{
    public static BagCancellationOutcome Evaluate(int? status, DateTime? transportFetchedAt)
    {
        if (status == 10)
        {
            return BagCancellationOutcome.AlreadyCancelled;
        }

        var isCancellableStatus = status is 0 or 1 or 2;
        if (!isCancellableStatus || transportFetchedAt.HasValue)
        {
            return BagCancellationOutcome.NotCancellable;
        }

        return BagCancellationOutcome.Cancelled;
    }
}
