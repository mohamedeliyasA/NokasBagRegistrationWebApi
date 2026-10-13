using Nokas.CashMgmt.Common.Correlation;
using Serilog.Context;

namespace Nokas.CashMgmt.Dk.Api.Middleware;

public sealed class CorrelationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[CorrelationConstants.HeaderName].ToString();

        if (string.IsNullOrEmpty(correlationId))
        {
            correlationId = Guid.NewGuid().ToString();
        }

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationConstants.HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        context.Items[CorrelationConstants.HttpContextItemsKey] = correlationId;

        using (LogContext.PushProperty(CorrelationConstants.LogPropertyName, correlationId))
        {
            await next(context);
        }
    }
}
