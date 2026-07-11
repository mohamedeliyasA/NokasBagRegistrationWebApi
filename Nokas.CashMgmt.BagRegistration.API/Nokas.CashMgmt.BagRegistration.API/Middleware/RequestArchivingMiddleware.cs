using System.Text;
using Nokas.CashMgmt.BagRegistration.API.Models;
using Nokas.CashMgmt.BagRegistration.API.Services.Queue;

namespace Nokas.CashMgmt.BagRegistration.API.Middleware
{
    public class RequestArchivingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IPayloadQueue _queue;
        private readonly ILogger<RequestArchivingMiddleware> _logger;

        public RequestArchivingMiddleware(RequestDelegate next, IPayloadQueue queue, ILogger<RequestArchivingMiddleware> logger)
        {
            _next = next;
            _queue = queue;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            // Only archive for our bag-registration endpoint: POST (create) and PUT/PATCH (update)
            if (context.Request.Path.StartsWithSegments("/api/cash-mgmt/bag-registration") &&
                (HttpMethods.IsPost(context.Request.Method) ||
                 HttpMethods.IsPut(context.Request.Method)))
            {
                var action = HttpMethods.IsPost(context.Request.Method) ? "create" : "update";

                try
                {
                    context.Request.EnableBuffering();
                    context.Request.Body.Position = 0;

                    string raw;
                    using (var reader = new StreamReader(
                               context.Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true))
                    {
                        raw = await reader.ReadToEndAsync();
                    }
                    context.Request.Body.Position = 0;

                    if (!string.IsNullOrWhiteSpace(raw))
                    {
                        var blobName = $"bag_{action}_{Guid.NewGuid():N}.json";
                        await _queue.EnqueueAsync(new PayloadBlobItem(blobName, raw));
                        context.Items["ArchivedBlobName"] = blobName;
                        _logger.LogInformation("Queued payload archive: {BlobName}", blobName);

                    }
                }
                catch (Exception ex)
                {
                    
                    _logger.LogError(ex, "Archiving middleware failed for {Method} {Path}", context.Request.Method, context.Request.Path);
                }
            }

            await _next(context);
        }

    }
}
