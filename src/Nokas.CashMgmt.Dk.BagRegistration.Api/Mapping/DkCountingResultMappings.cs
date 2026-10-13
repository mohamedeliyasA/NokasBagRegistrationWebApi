using Nokas.CashMgmt.Dk.BagRegistration.Api.Documents;
using Nokas.CashMgmt.Dk.Shared.Documents;
using Nokas.CashMgmt.Dk.BagRegistration.Api.Models;

namespace Nokas.CashMgmt.Dk.BagRegistration.Api.Mapping;

public static class DkCountingResultMappings
{
    public static DkCountingResultItemDto ToDto(this DkCountingResultDocument document) => new()
    {
        BagNumber = document.BagNumber,
        CustomerRefNumber = document.CustomerRefNumber,
        Status = document.Status,
        CurrencyType = document.CurrencyType,
        DeclaredAmount = document.DeclaredAmount,
        CountedAmount = document.CountedAmount,
        DeviationAmount = document.DeviationAmount,
        RegisteredAt = document.RegisteredAt,
        CountedAt = document.CountedAt,
        LastUpdatedAt = document.LastUpdatedAt,
        RecordId = document.ServiceNowNumber
    };
}
