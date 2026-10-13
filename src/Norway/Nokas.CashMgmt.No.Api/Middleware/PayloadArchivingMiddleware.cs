namespace Nokas.CashMgmt.No.Api.Middleware;

public sealed class PayloadArchivingMiddleware(RequestDelegate next, Services.IPayloadArchiveQueue queue)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (HttpMethods.IsPost(context.Request.Method) && context.Request.Path.StartsWithSegments("/api/cash-mgmt/bag-registration"))
        {
            context.Request.EnableBuffering();

            using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;

            var correlationId = context.Items[Nokas.CashMgmt.Common.Correlation.CorrelationConstants.HttpContextItemsKey]?.ToString() ?? "unknown";
            var blobName = $"bag_create_{Guid.NewGuid():N}.json";

            await queue.EnqueueAsync(new Services.PayloadArchiveItem(blobName, body, correlationId));
            context.Items["ArchivedBlobName"] = blobName;
        }

        await next(context);
    }
}