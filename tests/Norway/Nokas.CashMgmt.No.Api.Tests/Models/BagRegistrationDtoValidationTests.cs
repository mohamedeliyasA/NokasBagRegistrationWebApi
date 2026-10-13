using System.ComponentModel.DataAnnotations;
using Nokas.CashMgmt.No.Api.Models;
using Xunit;

namespace Nokas.CashMgmt.No.Api.Tests.Models;

public class BagRegistrationDtoValidationTests
{
    private static List<ValidationResult> Validate(BagRegistrationDto dto)
    {
        var context = new ValidationContext(dto);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(dto, context, results, validateAllProperties: true);
        results.AddRange(dto.Validate(context));
        return results;
    }

    [Fact]
    public void Nok_WithTotalOrderAmount_IsValid()
    {
        var dto = new BagRegistrationDto
        {
            CustomerRefNumber = "123",
            RegistrationType = "NOK",
            MessageId = Guid.NewGuid().ToString(),
            BagNumber = "999",
            TotalOrderAmount = "5000"
        };

        Assert.Empty(Validate(dto));
    }

    [Fact]
    public void Nok_WithoutTotalOrderAmount_IsInvalid()
    {
        var dto = new BagRegistrationDto
        {
            CustomerRefNumber = "123",
            RegistrationType = "NOK",
            MessageId = Guid.NewGuid().ToString(),
            BagNumber = "999"
        };

        var results = Validate(dto);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(BagRegistrationDto.TotalOrderAmount)));
    }

    [Fact]
    public void Valuta_IsAlwaysRejected()
    {
        var dto = new BagRegistrationDto
        {
            CustomerRefNumber = "123",
            RegistrationType = "Valuta",
            MessageId = Guid.NewGuid().ToString(),
            BagNumber = "999",
            BagItemsFC = [new BagItemFcDto { ItemCurrencyCode = "USD" }]
        };

        var results = Validate(dto);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(BagRegistrationDto.RegistrationType)));
    }
}