using Nokas.CashMgmt.Shared.StatusMapping;
using Xunit;

namespace Nokas.CashMgmt.BagRegistration.Api.Tests.StatusMapping;

public class NattsafeStatusMapperTests
{
    [Theory]
    [InlineData(0, "registered")]
    [InlineData(1, "registered")]
    [InlineData(2, "in-progress")]
    [InlineData(3, "picked-up")]
    [InlineData(4, "received by bank")]
    [InlineData(5, "counted")]
    [InlineData(8, "error")]
    [InlineData(9, "error")]
    [InlineData(-1, "error")]
    [InlineData(10, "canceled")]
    [InlineData(11, "not approved")]
    [InlineData(12, "approved by bank")]
    [InlineData(13, "missing")]
    [InlineData(999, "unknown")]
    [InlineData(null, "unknown")]
    public void ToLabel_MapsRawCodeToExpectedLabel(int? statusCode, string expectedLabel)
    {
        var label = NattsafeStatusMapper.ToLabel(statusCode);

        Assert.Equal(expectedLabel, label);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ToLabel_TransportFetchedOverridesRegisteredAndInProgress(int statusCode)
    {
        var label = NattsafeStatusMapper.ToLabel(statusCode, transportFetchedAt: DateTime.UtcNow);

        Assert.Equal("picked-up", label);
    }

    [Fact]
    public void ToLabel_TransportFetchedDoesNotOverrideOtherStatuses()
    {
        var label = NattsafeStatusMapper.ToLabel(5, transportFetchedAt: DateTime.UtcNow);

        Assert.Equal("counted", label);
    }
}
