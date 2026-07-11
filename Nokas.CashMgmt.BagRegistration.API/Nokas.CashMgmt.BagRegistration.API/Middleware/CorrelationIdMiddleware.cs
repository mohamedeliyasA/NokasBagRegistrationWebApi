namespace Nokas.CashMgmt.BagRegistration.API.Middleware
{
    public class CorrelationIdMiddleware
    {
        private readonly RequestDelegate _next;

        public CorrelationIdMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Check if the correlation ID is passed by the client
            var correlationId = context.Request.Headers["X-Correlation-ID"].ToString();

            // If no correlation ID is provided, generate a new one
            if (string.IsNullOrEmpty(correlationId))
            {
                correlationId = Guid.NewGuid().ToString();
            }

            // Set the correlation ID in the response headers and logging context
            context.Response.OnStarting(() =>
            {
                context.Response.Headers["X-Correlation-ID"] = correlationId;
                return Task.CompletedTask;
            });

            // Set the correlation ID in the current request context
            context.Items["CorrelationId"] = correlationId;

            // Continue the request pipeline
            await _next(context);
        }
    }
}
