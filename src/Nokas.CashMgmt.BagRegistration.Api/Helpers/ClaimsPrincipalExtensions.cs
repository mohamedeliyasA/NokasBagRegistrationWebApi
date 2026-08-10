using System.Security.Claims;

namespace Nokas.CashMgmt.BagRegistration.Api.Helpers;

public static class ClaimsPrincipalExtensions
{
    public static string? GetClientId(this ClaimsPrincipal user) =>
        user.Claims.FirstOrDefault(c => c.Type is "azp" or "appid")?.Value;
}