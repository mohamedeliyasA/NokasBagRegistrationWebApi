using System.Security.Claims;

namespace Nokas.CashMgmt.BagRegistration.API.Helpers
{
    public static class ClaimsHelper
    {
        public static string? GetClientId(ClaimsPrincipal user)
        {
            return user?.Claims.FirstOrDefault(c =>
                 c.Type == "azp" || c.Type == "appid")?.Value;
        }

        public static string? GetUserId(ClaimsPrincipal user)
        {
            return user?.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
        }

        public static string? GetEmail(ClaimsPrincipal user)
        {
            return user?.Claims.FirstOrDefault(c => c.Type == "emails")?.Value;
        }
    }
}
