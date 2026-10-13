using Nokas.CashMgmt.No.Api.Domain;
using Xunit;

namespace Nokas.CashMgmt.No.Api.Tests.Domain;

public class BagCancellationDecisionTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Evaluate_CancellableStatusWithNoTransportFetch_ReturnsCancelled(int status)
    {
        var outcome = BagCancellationDecision.Evaluate(status, transportFetchedAt: null);

        Assert.Equal(BagCancellationOutcome.Cancelled, outcome);
    }

    [Fact]
    public void Evaluate_AlreadyCancelledStatus_ReturnsAlreadyCancelled()
    {
        var outcome = BagCancellationDecision.Evaluate(10, transportFetchedAt: null);

        Assert.Equal(BagCancellationOutcome.AlreadyCancelled, outcome);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Evaluate_CancellableStatusButAlreadyTransportFetched_ReturnsNotCancellable(int status)
    {
        var outcome = BagCancellationDecision.Evaluate(status, transportFetchedAt: DateTime.UtcNow);

        Assert.Equal(BagCancellationOutcome.NotCancellable, outcome);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(-1)]
    [InlineData(null)]
    public void Evaluate_NonCancellableStatus_ReturnsNotCancellable(int? status)
    {
        var outcome = BagCancellationDecision.Evaluate(status, transportFetchedAt: null);

        Assert.Equal(BagCancellationOutcome.NotCancellable, outcome);
    }
}
