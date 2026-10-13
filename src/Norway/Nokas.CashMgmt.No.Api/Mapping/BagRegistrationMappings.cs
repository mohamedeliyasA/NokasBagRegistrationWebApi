using Nokas.CashMgmt.No.Api.Documents;
using Nokas.CashMgmt.No.Api.Models;

namespace Nokas.CashMgmt.No.Api.Mapping;

public static class BagRegistrationMappings
{
    public static BagRegistrationDocument ToDocument(
        this BagRegistrationDto dto,
        string clientId,
        string bagLifecycleId,
        string correlationId)
    {
        var registeredBy = $"API_{clientId}";

        return new BagRegistrationDocument
        {
            Id = dto.BagNumber,
            ClientId = clientId,
            BagLifecycleId = bagLifecycleId,
            CorrelationId = correlationId,
            RegistrationStatus = "In-Progress",
            BagRegistration = new BagRegistrationDetail
            {
                CustomerRefNumber = dto.CustomerRefNumber,
                RegistrationType = dto.RegistrationType,
                MessageId = dto.MessageId,
                BagNumber = dto.BagNumber,
                RegisteredBy = registeredBy,
                RegistrationDate = dto.RegistrationDate,
                TurnOverDate = dto.TurnOverDate,
                RegisteredNotes = dto.RegisteredNotes,
                RegisteredCoins = dto.RegisteredCoins,
                RegisteredChecks = dto.RegisteredChecks,
                TotalOrderAmount = dto.TotalOrderAmount
            },
            OperationHistory =
            [
                new OperationHistoryEntry
                {
                    Action = "Create",
                    Timestamp = DateTime.UtcNow,
                    BagLifecycleId = bagLifecycleId,
                    CorrelationId = correlationId,
                    PerformedBy = clientId
                }
            ]
        };
    }
}