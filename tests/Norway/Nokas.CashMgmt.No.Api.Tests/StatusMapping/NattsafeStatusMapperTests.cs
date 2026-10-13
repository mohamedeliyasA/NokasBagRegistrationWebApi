using Nokas.CashMgmt.No.Shared.StatusMapping;
using Xunit;

namespace Nokas.CashMgmt.No.Api.Tests.StatusMapping;

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

    [Theory]
    [InlineData(0, "registered")]
    [InlineData(1, "registered")]
    [InlineData(2, "in-progress")]
    [InlineData(3, "picked-up")]
    [InlineData(4, "in-progress")]
    [InlineData(5, "counted")]
    [InlineData(8, "error")]
    [InlineData(9, "error")]
    [InlineData(-1, "error")]
    [InlineData(10, "cancelled")]
    [InlineData(11, "error")]
    [InlineData(12, "in-progress")]
    [InlineData(13, "error")]
    [InlineData(999, "unknown")]
    [InlineData(null, "unknown")]
    public void ToBucket_MapsRawCodeToExpectedBucket(int? statusCode, string expectedBucket)
    {
        var bucket = NattsafeStatusMapper.ToBucket(statusCode);

        Assert.Equal(expectedBucket, bucket);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ToBucket_TransportFetchedOverridesRegisteredAndInProgress(int statusCode)
    {
        var bucket = NattsafeStatusMapper.ToBucket(statusCode, transportFetchedAt: DateTime.UtcNow);

        Assert.Equal("picked-up", bucket);
    }

    [Theory]
    [InlineData("registered")]
    [InlineData("in-progress")]
    [InlineData("picked-up")]
    [InlineData("counted")]
    [InlineData("cancelled")]
    [InlineData("error")]
    public void GranularLabelsForBucket_EveryValidBucketResolvesWithoutThrowing(string bucket)
    {
        Assert.Contains(bucket, NattsafeStatusMapper.ValidBuckets);
        var labels = NattsafeStatusMapper.GranularLabelsForBucket(bucket);
        Assert.NotEmpty(labels);
    }

    [Fact]
    public void GranularLabelsForBucket_UnknownBucketThrows()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NattsafeStatusMapper.GranularLabelsForBucket("not-a-bucket"));
    }

    [Theory]
    [InlineData(0, null, "registered")]
    [InlineData(2, null, "in-progress")]
    [InlineData(4, null, "in-progress")]
    [InlineData(12, null, "in-progress")]
    [InlineData(3, null, "picked-up")]
    [InlineData(5, null, "counted")]
    [InlineData(10, null, "cancelled")]
    [InlineData(11, null, "error")]
    [InlineData(13, null, "error")]
    public void ToLabel_ThenGranularLabelsForBucket_RoundTripsToTheSameBucketAsToBucket(int statusCode, DateTime? transportFetchedAt, string expectedBucket)
    {
        var label = NattsafeStatusMapper.ToLabel(statusCode, transportFetchedAt);
        var bucket = NattsafeStatusMapper.ToBucket(statusCode, transportFetchedAt);

        Assert.Equal(expectedBucket, bucket);
        Assert.Contains(label, NattsafeStatusMapper.GranularLabelsForBucket(bucket));
    }
}
