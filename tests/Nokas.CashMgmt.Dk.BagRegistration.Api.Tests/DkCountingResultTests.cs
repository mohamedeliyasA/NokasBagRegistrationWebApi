using Microsoft.AspNetCore.DataProtection;
using Nokas.CashMgmt.Dk.Shared.Documents;
using Nokas.CashMgmt.Dk.BagRegistration.Api.Mapping;
using Nokas.CashMgmt.Dk.BagRegistration.Api.Services;
using Nokas.CashMgmt.Dk.Shared.Services;

namespace Nokas.CashMgmt.Dk.BagRegistration.Api.Tests;

public class DkCountingResultTests
{
    [Theory]
    [InlineData("1", "registered")]
    [InlineData("10", "counted")]
    [InlineData("New", "registered")]
    [InlineData("Paid out", "counted")]
    [InlineData("Work in Progress", "in-progress")]
    [InlineData("work in progress", "in-progress")]
    [InlineData("Draft", "registered")]
    [InlineData("Registered", "registered")]
    [InlineData("Ordered", "picked-up")]
    [InlineData("Packed", "picked-up")]
    [InlineData("In transit", "picked-up")]
    [InlineData("In cash center", "in-progress")]
    [InlineData("In cash depot", "in-progress")]
    [InlineData("On Hold", "in-progress")]
    [InlineData("Counted", "counted")]
    [InlineData("Closed", "counted")]
    [InlineData("Completed", "counted")]
    [InlineData("Cancelled", "cancelled")]
    [InlineData("Error", "error")]
    [InlineData("paid out", "counted")]
    [InlineData("7", "unknown")]
    [InlineData("", "unknown")]
    [InlineData(null, "unknown")]
    public void StatusMapper_MapsKnownStatesAndFoldsRestToUnknown(string? raw, string expected) =>
        Assert.Equal(expected, DkCountingResultStatusMapper.ToBucket(raw));

    [Fact]
    public void Token_RoundTrips()
    {
        var svc = new ContinuationTokenService(new EphemeralDataProtectionProvider());
        var hash = svc.ComputeQueryHash("c", DateTime.UnixEpoch, DateTime.UnixEpoch.AddDays(1), "lastUpdated", null, null, 100);
        var payload = svc.TryUnwrap(svc.Wrap("native", hash));
        Assert.NotNull(payload);
        Assert.Equal("native", payload!.NativeToken);
        Assert.Equal(hash, payload.QueryHash);
    }

    [Fact]
    public void Token_GarbageIsRejected() =>
        Assert.Null(new ContinuationTokenService(new EphemeralDataProtectionProvider()).TryUnwrap("not-a-token"));

    [Fact]
    public void QueryHash_ChangesWithAnyParameter()
    {
        var svc = new ContinuationTokenService(new EphemeralDataProtectionProvider());
        var from = DateTime.UnixEpoch;
        var to = from.AddDays(1);
        var baseHash = svc.ComputeQueryHash("c", from, to, "lastUpdated", null, null, 100);
        Assert.NotEqual(baseHash, svc.ComputeQueryHash("c", from, to, "lastUpdated", "counted", null, 100));
        Assert.NotEqual(baseHash, svc.ComputeQueryHash("c", from, to, "lastUpdated", null, "3818", 100));
        Assert.NotEqual(baseHash, svc.ComputeQueryHash("c", from, to, "lastUpdated", null, null, 50));
        Assert.NotEqual(baseHash, svc.ComputeQueryHash("other", from, to, "lastUpdated", null, null, 100));
        Assert.NotEqual(baseHash, svc.ComputeQueryHash("c", from, to, "registered", null, null, 100));
    }

    [Fact]
    public void EveryServiceNowStateIsMapped()
    {
        // The complete list ServiceNow supplied on 2026-09-23. If they add one, it shows up as
        // "unknown" in this test before a partner ever sees it.
        string[] all =
        [
            "Paid out", "In cash center", "In cash depot", "In transit", "Ordered", "Packed",
            "Registered", "Draft", "New", "Work in Progress", "On Hold", "Counted", "Closed",
            "Cancelled", "Completed", "Error"
        ];

        Assert.All(all, state => Assert.NotEqual("unknown", DkCountingResultStatusMapper.ToBucket(state)));
    }

    [Fact]
    public void Mapping_UsesServiceNowNumberAsRecordId()
    {
        var doc = new DkCountingResultDocument
        {
            Id = "NOC1", ClientId = "c", CustomerRefNumber = "3818", ServiceNowNumber = "NOC1",
            Status = "counted", LastUpdatedAt = DateTime.UnixEpoch
        };
        var dto = doc.ToDto();
        Assert.Equal("NOC1", dto.RecordId);
        Assert.Equal("DKK", dto.CurrencyType);
    }
}

public class DkResponseShapeTests
{
    private static readonly System.Text.Json.JsonSerializerOptions ApiOptions = new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    [Fact]
    public void UncountedItem_StillCarriesEveryField_AsNull()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new Nokas.CashMgmt.Dk.BagRegistration.Api.Models.DkCountingResultItemDto
        {
            CustomerRefNumber = "4200000198", Status = "registered", CurrencyType = "DKK",
            LastUpdatedAt = DateTime.UnixEpoch, RecordId = "NOC1"
        }, ApiOptions);

        foreach (var field in new[] { "bagNumber", "declaredAmount", "countedAmount", "deviationAmount", "registeredAt", "countedAt" })
        {
            Assert.Contains($"\"{field}\":null", json);
        }
    }

    [Fact]
    public void LastPage_StillCarriesContinuationToken_AsNull()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(
            new Nokas.CashMgmt.Dk.BagRegistration.Api.Models.DkPaginationDto { PageSize = 100, Count = 0, HasMore = false }, ApiOptions);
        Assert.Contains("\"continuationToken\":null", json);
    }
}

public class DkMetaDataValidationTests
{
    private static string Body(string? metaJson) =>
        $$"""
        {
          "bagRegistration": {
            "customerRefNumber": "4200000198", "cashRegisterId": "POS-01",
            "messageId": "0f8fad5b-d9cb-469f-a165-70867728950e", "bagNumber": "420019800002",
            "registrationDate": "2026-09-22", "totalOrderAmount": "700.00"
          }{{(metaJson is null ? "" : ", \"metaData\": " + metaJson)}}
        }
        """;

    private static Nokas.CashMgmt.Dk.BagRegistration.Api.Validation.DkValidationResult Validate(string? meta) =>
        Nokas.CashMgmt.Dk.BagRegistration.Api.Validation.DkBagRegistrationValidator.Validate(Body(meta), false);

    [Fact]
    public void AnyNameValuePairs_AreAccepted()
    {
        var result = Validate("""{"companyId":"3214","branchId":"9543","tillRef":"A/7"}""");
        Assert.True(result.IsValid, result.JoinedMessages);
        Assert.Equal("3214", result.Fields!.MetaData!["companyId"]);
        Assert.Equal("9543", result.Fields.MetaData["branchId"]);
        Assert.Equal("A/7", result.Fields.MetaData["tillRef"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("null")]
    [InlineData("{}")]
    public void Absent_Empty_OrNull_IsValidWithNoMetaData(string? meta)
    {
        var result = Validate(meta);
        Assert.True(result.IsValid, result.JoinedMessages);
        Assert.Null(result.Fields!.MetaData);
    }

    [Fact]
    public void NonStringValue_IsRejected_NamingThePath()
    {
        var result = Validate("""{"branchId":9543}""");
        Assert.False(result.IsValid);
        Assert.Equal("metaData.branchId", result.Failures[0].Field);
    }

    [Fact]
    public void NotAnObject_IsRejected() => Assert.False(Validate("\"9543\"").IsValid);

    [Fact]
    public void TooManyEntries_IsRejected()
    {
        var many = string.Join(",", Enumerable.Range(0, 21).Select(i => $"\"k{i}\":\"v\""));
        Assert.False(Validate("{" + many + "}").IsValid);
    }

    [Fact]
    public void OverlongValue_IsRejected() =>
        Assert.False(Validate($$"""{"branchId":"{{new string('9', 201)}}"}""").IsValid);

    [Fact]
    public void UnknownRootField_IsStillRejected()
    {
        var result = Nokas.CashMgmt.Dk.BagRegistration.Api.Validation.DkBagRegistrationValidator.Validate(
            """{"bagRegistration":{},"somethingElse":{}}""", false);
        Assert.Contains(result.Failures, f => f.Field == "somethingElse");
    }
}

public class DkDateRangeTests
{
    // Mirrors the controller rule: a date-only toDate covers that whole day.
    private static DateTime EndOfDayIfDateOnly(DateTime toDate) =>
        toDate.TimeOfDay == TimeSpan.Zero ? toDate.AddDays(1).AddTicks(-1) : toDate;

    [Fact]
    public void DateOnlyToDate_CoversTheWholeDay()
    {
        var end = EndOfDayIfDateOnly(new DateTime(2026, 9, 23, 0, 0, 0, DateTimeKind.Utc));
        Assert.True(end > new DateTime(2026, 9, 23, 23, 59, 58, DateTimeKind.Utc));
        Assert.True(end < new DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void ExplicitTime_IsLeftExactlyAsGiven()
    {
        var exact = new DateTime(2026, 9, 23, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal(exact, EndOfDayIfDateOnly(exact));
    }
}

public class ServiceNowClockTests
{
    private static readonly ServiceNowClock Clock = new("Central European Standard Time");

    [Fact]
    public void SummerTimestamp_IsTwoHoursAheadOfUtc()
    {
        // The real case that proved the offset: a bag registered 06:14:04 UTC on 2026-09-23 came
        // back from ServiceNow as "2026-09-23 08:14:04".
        Assert.True(Clock.TryParse("2026-09-23 08:14:04", out var utc));
        Assert.Equal(new DateTime(2026, 9, 23, 6, 14, 4, DateTimeKind.Utc), utc);
    }

    [Fact]
    public void WinterTimestamp_IsOneHourAheadOfUtc()
    {
        Assert.True(Clock.TryParse("2026-01-15 09:00:00", out var utc));
        Assert.Equal(new DateTime(2026, 1, 15, 8, 0, 0, DateTimeKind.Utc), utc);
    }

    [Fact]
    public void QueryString_ConvertsUtcToTheirWallClock() =>
        Assert.Equal("2026-09-25 08:26:00", Clock.ToQueryString(new DateTime(2026, 9, 25, 6, 26, 0, DateTimeKind.Utc)));

    [Fact]
    public void QueryString_AndParse_RoundTrip()
    {
        var utc = new DateTime(2026, 9, 25, 6, 26, 0, DateTimeKind.Utc);
        Assert.True(Clock.TryParse(Clock.ToQueryString(utc), out var back));
        Assert.Equal(utc, back);
    }

    [Fact]
    public void DateOnlyValue_IsAccepted()
    {
        Assert.True(Clock.TryParse("2026-09-23", out var utc));
        Assert.Equal(new DateTime(2026, 9, 22, 22, 0, 0, DateTimeKind.Utc), utc);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a date")]
    public void UnparseableValue_IsRejected(string? raw) => Assert.False(Clock.TryParse(raw, out _));

    [Fact]
    public void UnknownZone_FallsBackRatherThanThrowing() =>
        Assert.True(new ServiceNowClock("No/Such_Zone").TryParse("2026-09-23 08:14:04", out _));
}

public class DkSyncWindowTests
{
    // Mirrors the sync's window rule: start at the watermark minus the overlap, or the initial
    // lookback when there is no watermark yet.
    private static DateTime WindowStart(DateTime? watermark, DateTime now, int overlapMinutes, int initialLookbackDays) =>
        watermark is null ? now.AddDays(-initialLookbackDays) : watermark.Value.AddMinutes(-overlapMinutes);

    [Fact]
    public void FirstRun_ReachesBackTheInitialLookback()
    {
        var now = new DateTime(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal(now.AddDays(-120), WindowStart(null, now, 10, 120));
    }

    [Fact]
    public void LaterRun_StartsBeforeTheWatermark_SoNothingFallsBetweenWindows()
    {
        var now = new DateTime(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);
        var watermark = now.AddMinutes(-2);
        var start = WindowStart(watermark, now, 10, 120);

        Assert.True(start < watermark);
        Assert.Equal(now.AddMinutes(-12), start);
    }
}
