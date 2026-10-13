using Nokas.CashMgmt.Dk.Api.Services;
using Nokas.CashMgmt.Common.Correlation;

namespace Nokas.CashMgmt.Dk.Api.Middleware;

// Buffers the request body so the controller can read it after this middleware has, and
// queues the raw bytes for Blob archiving. Archiving is deliberately fire-and-forget:
// the partner's response must not depend on storage.
public sealed class PayloadArchivingMiddleware(RequestDelegate next, IPayloadArchiveQueue queue)
{
    public const string ArchivedBlobNameItemKey = "ArchivedBlobName";

    private const string ArchivedPath = "/api/cash-mgmt/dk/bag-registration";

    public async Task InvokeAsync(HttpContext context)
    {
        if (HttpMethods.IsPost(context.Request.Method) &&
            context.Request.Path.StartsWithSegments(ArchivedPath))
        {
            context.Request.EnableBuffering();

            using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;

            var correlationId = context.Items[CorrelationConstants.HttpContextItemsKey]?.ToString() ?? "unknown";
            var blobName = $"dk_bag_create_{DateTime.UtcNow:yyyyMMdd'T'HHmmss}_{Guid.NewGuid():N}.json";

            await queue.EnqueueAsync(new PayloadArchiveItem(blobName, body, correlationId));
            context.Items[ArchivedBlobNameItemKey] = blobName;
        }

        await next(context);
    }
}
