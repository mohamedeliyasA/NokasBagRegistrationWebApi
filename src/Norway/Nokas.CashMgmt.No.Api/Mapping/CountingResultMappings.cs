using Nokas.CashMgmt.No.Api.Models;
using Nokas.CashMgmt.No.Shared.Documents;
using Nokas.CashMgmt.No.Shared.StatusMapping;

namespace Nokas.CashMgmt.No.Api.Mapping;

public static class CountingResultMappings
{
    public static CountingResultItemDto ToDto(this CountingResultDocument document) => new()
    {
        BagNumber = document.BagNumber,
        CustomerRefNumber = document.CustomerRefNumber,
        Status = NattsafeStatusMapper.ToBucket(document.StatusCode, document.TransportFetchedAt),
        CurrencyType = document.CurrencyType,
        DeclaredAmount = document.DeclaredAmount,
        CountedAmount = document.CountedAmount,
        DeviationAmount = document.DeviationAmount,
        RegisteredAt = document.RegisteredAt,
        CountedAt = document.CountedAt,
        LastUpdatedAt = document.LastUpdatedAt,
        StatusHistory = [.. document.StatusHistory.Select(h => new CountingResultStatusHistoryItemDto
        {
            Status = h.Status,
            ChangedAt = h.ChangedAt
        })]
    };
}
