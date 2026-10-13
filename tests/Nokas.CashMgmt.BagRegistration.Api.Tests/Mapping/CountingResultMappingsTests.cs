using Nokas.CashMgmt.BagRegistration.Api.Mapping;
using Nokas.CashMgmt.Shared.Documents;
using Xunit;

namespace Nokas.CashMgmt.BagRegistration.Api.Tests.Mapping;

public class CountingResultMappingsTests
{
    [Fact]
    public void ToDto_MapsFieldsAndFoldsStatusIntoBucket()
    {
        var document = new CountingResultDocument
        {
            Id = "999",
            ClientId = "client-1",
            NattsafeRecordId = 999,
            BagNumber = "20250822162",
            CustomerRefNumber = "5790002334764",
            CustomerNumber = "1001",
            Status = "received by bank", // granular label
            StatusCode = 4,
            CurrencyType = "NOK",
            DeclaredAmount = 5000m,
            CountedAmount = null,
            DeviationAmount = null,
            RegisteredAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CountedAt = null,
            LastUpdatedAt = new DateTime(2026, 1, 1, 1, 0, 0, DateTimeKind.Utc),
            StatusHistory =
            [
                new CountingResultStatusHistoryEntry { StatusCode = 0, Status = "registered", ChangedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new CountingResultStatusHistoryEntry { StatusCode = 4, Status = "received by bank", ChangedAt = new DateTime(2026, 1, 1, 1, 0, 0, DateTimeKind.Utc) }
            ]
        };

        var dto = document.ToDto();

        Assert.Equal("20250822162", dto.BagNumber);
        Assert.Equal("5790002334764", dto.CustomerRefNumber);
        Assert.Equal("in-progress", dto.Status); // granular "received by bank" folds into the "in-progress" bucket
        Assert.Equal("NOK", dto.CurrencyType);
        Assert.Equal(5000m, dto.DeclaredAmount);
        Assert.Null(dto.CountedAmount);
        Assert.Null(dto.DeviationAmount);
        Assert.Equal(2, dto.StatusHistory.Count);
        Assert.Equal("registered", dto.StatusHistory[0].Status);
    }

    [Fact]
    public void ToDto_TransportFetchedOverridesBucketToPickedUp()
    {
        var document = new CountingResultDocument
        {
            Id = "1000",
            ClientId = "client-1",
            NattsafeRecordId = 1000,
            BagNumber = "20250822163",
            CustomerRefNumber = "5790002334764",
            CustomerNumber = "1001",
            Status = "picked-up",
            StatusCode = 1,
            TransportFetchedAt = DateTime.UtcNow,
            LastUpdatedAt = DateTime.UtcNow
        };

        var dto = document.ToDto();

        Assert.Equal("picked-up", dto.Status);
    }
}
