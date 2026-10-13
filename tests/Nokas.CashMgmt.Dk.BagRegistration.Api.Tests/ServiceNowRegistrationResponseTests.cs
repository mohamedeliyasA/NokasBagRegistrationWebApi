using Nokas.CashMgmt.Dk.BagRegistration.Api.Services;

namespace Nokas.CashMgmt.Dk.BagRegistration.Api.Tests;

public class ServiceNowRegistrationResponseTests
{
    private const string Success = """{"result":{"status":"success","message_id":"abc","message":"Bag registration successfull. NOC0430515 was created."}}""";
    private const string SuccessSysId = """{"result":{"status":"success","sys_id":"xyz","message":"ok"}}""";
    private const string Failure = """{"error":{"message":"Error: Unknown source abe243be (line 21)","detail":""},"status":"failure"}""";

    [Fact]
    public void Parse_Success_ReadsMessageAndId()
    {
        var p = ServiceNowRegistrationResponse.Parse(Success)!;
        Assert.Equal("success", p.Status);
        Assert.Equal("abc", p.RecordId);
        Assert.Equal("Bag registration successfull. NOC0430515 was created.", p.Message);
    }

    [Fact]
    public void Parse_AcceptsSysIdAsWellAsMessageId() =>
        Assert.Equal("xyz", ServiceNowRegistrationResponse.Parse(SuccessSysId)!.RecordId);

    [Fact]
    public void Parse_Failure_ReadsErrorMessageAndStatus()
    {
        var p = ServiceNowRegistrationResponse.Parse(Failure)!;
        Assert.Equal("failure", p.Status);
        Assert.Contains("Unknown source", p.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    public void Parse_UnreadableBody_ReturnsNull(string? body) =>
        Assert.Null(ServiceNowRegistrationResponse.Parse(body));

    private const string BagLabel = """{"result":{"message":"The bag label number must belong to the account of the registration.","message_id":"d99","status":"failed"}}""";

    [Fact]
    public void Classify_BusinessFailureUnderHttp500_IsRejectedWithItsMessage()
    {
        var p = ServiceNowRegistrationResponse.Parse(BagLabel)!;
        Assert.Equal(ServiceNowOutcome.Rejected, ServiceNowRegistrationResponse.Classify(500, p));
        Assert.Equal("The bag label number must belong to the account of the registration.", p.Message);
    }

    [Fact]
    public void Classify_AlreadyRegisteredLabel_IsDuplicate()
    {
        const string body = """{"result":{"message":"The bag label number has already been registered and can not be used.","message_id":"1b4","status":"failed"}}""";
        Assert.Equal(ServiceNowOutcome.Duplicate, ServiceNowRegistrationResponse.Classify(500, ServiceNowRegistrationResponse.Parse(body)));
    }

    [Fact]
    public void Classify_TheirOwnInternalError_IsBackendError_NotThePartnersFault()
    {
        // Observed on the TEST instance 2026-09-28. Same shape as a real refusal, but nothing the
        // partner sends could fix it - so it must not come back as a 400.
        const string body = """{"result":{"message":"Internal error. Please contact Nokas Værdihåndtering A/S +45 70157600.","message_id":"7ff","status":"failed"}}""";
        Assert.Equal(ServiceNowOutcome.BackendError, ServiceNowRegistrationResponse.Classify(500, ServiceNowRegistrationResponse.Parse(body)));
    }

    [Fact]
    public void Classify_PlatformErrorUnderHttp500_StaysBackendError() =>
        Assert.Equal(ServiceNowOutcome.BackendError, ServiceNowRegistrationResponse.Classify(500, ServiceNowRegistrationResponse.Parse(Failure)));

    [Fact]
    public void Classify_Success_IsNotABusinessFailure() =>
        Assert.False(ServiceNowRegistrationResponse.Parse(Success)!.BusinessFailure);

    [Fact]
    public void Classify_2xxWithSuccess_IsSuccess() =>
        Assert.Equal(ServiceNowOutcome.Success, ServiceNowRegistrationResponse.Classify(200, ServiceNowRegistrationResponse.Parse(Success)));

    [Fact]
    public void Classify_2xxUnreadable_IsNeverSuccess() =>
        Assert.Equal(ServiceNowOutcome.BackendError, ServiceNowRegistrationResponse.Classify(200, null));

    [Fact]
    public void Classify_2xxWithFailureStatus_IsRejected() =>
        Assert.Equal(ServiceNowOutcome.Rejected, ServiceNowRegistrationResponse.Classify(200, ServiceNowRegistrationResponse.Parse(Failure)));

    [Theory]
    [InlineData(409, ServiceNowOutcome.Duplicate)]
    [InlineData(400, ServiceNowOutcome.Rejected)]
    [InlineData(404, ServiceNowOutcome.Rejected)]
    [InlineData(422, ServiceNowOutcome.Rejected)]
    [InlineData(401, ServiceNowOutcome.BackendError)]
    [InlineData(403, ServiceNowOutcome.BackendError)]
    [InlineData(500, ServiceNowOutcome.BackendError)]
    [InlineData(503, ServiceNowOutcome.BackendError)]
    public void Classify_ErrorStatuses(int status, ServiceNowOutcome expected) =>
        Assert.Equal(expected, ServiceNowRegistrationResponse.Classify(status, ServiceNowRegistrationResponse.Parse(Failure)));
}
