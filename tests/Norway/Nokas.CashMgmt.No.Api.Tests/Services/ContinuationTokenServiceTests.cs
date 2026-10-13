using Microsoft.AspNetCore.DataProtection;
using Nokas.CashMgmt.No.Api.Services;
using Xunit;

namespace Nokas.CashMgmt.No.Api.Tests.Services;

public class ContinuationTokenServiceTests
{
    private static ContinuationTokenService CreateService() =>
        new(new EphemeralDataProtectionProvider());

    [Fact]
    public void WrapThenTryUnwrap_RoundTripsNativeTokenAndQueryHash()
    {
        var service = CreateService();
        var hash = service.ComputeQueryHash("client-1", DateTime.UtcNow.Date, DateTime.UtcNow, "counted", "counted", "1234", 100);

        var wrapped = service.Wrap("native-cosmos-token", hash);
        var unwrapped = service.TryUnwrap(wrapped);

        Assert.NotNull(unwrapped);
        Assert.Equal("native-cosmos-token", unwrapped!.NativeToken);
        Assert.Equal(hash, unwrapped.QueryHash);
    }

    [Fact]
    public void TryUnwrap_MalformedTokenReturnsNull()
    {
        var service = CreateService();

        var result = service.TryUnwrap("not-a-real-token!!!");

        Assert.Null(result);
    }

    [Fact]
    public void TryUnwrap_TokenFromADifferentProviderReturnsNull()
    {
        var serviceA = CreateService();
        var serviceB = CreateService();

        var wrapped = serviceA.Wrap("native-token", "hash");
        var result = serviceB.TryUnwrap(wrapped);

        Assert.Null(result);
    }

    [Fact]
    public void ComputeQueryHash_DifferentParametersProduceDifferentHashes()
    {
        var service = CreateService();
        var fromDate = DateTime.UtcNow.Date;
        var toDate = DateTime.UtcNow;

        var hash1 = service.ComputeQueryHash("client-1", fromDate, toDate, "counted", null, null, 100);
        var hash2 = service.ComputeQueryHash("client-1", fromDate, toDate, "counted", null, null, 200);

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void ComputeQueryHash_SameParametersProduceTheSameHash()
    {
        var service = CreateService();
        var fromDate = DateTime.UtcNow.Date;
        var toDate = DateTime.UtcNow;

        var hash1 = service.ComputeQueryHash("client-1", fromDate, toDate, "counted", "registered", "1234", 100);
        var hash2 = service.ComputeQueryHash("client-1", fromDate, toDate, "counted", "registered", "1234", 100);

        Assert.Equal(hash1, hash2);
    }
}
