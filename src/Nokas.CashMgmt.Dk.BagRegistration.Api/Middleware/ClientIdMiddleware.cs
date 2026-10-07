using Serilog.Context;

namespace Nokas.CashMgmt.Dk.BagRegistration.Api.Middleware;

public sealed class ClientIdMiddleware(RequestDelegate next)
{
    private const string LogPropertyName = "ClientId";

    public async Task InvokeAsync(HttpContext context)
    {
        var clientId = context.User.Claims.FirstOrDefault(c => c.Type is "azp" or "appid")?.Value;

        if (string.IsNullOrEmpty(clientId))
        {
            await next(context);
            return;
        }

        using (LogContext.PushProperty(LogPropertyName, clientId))
        {
            await next(context);
        }
    }
}
