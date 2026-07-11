using System.Net;
using System.Text.Json;
using System.Xml.Serialization;
using Microsoft.Azure.Cosmos;
using Nokas.CashMgmt.BagRegistration.API.Helpers;
using Nokas.CashMgmt.BagRegistration.API.Models;

namespace Nokas.CashMgmt.BagRegistration.API.Middleware
{
    public class ExceptionLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionLoggingMiddleware> _logger;
        private readonly IWebHostEnvironment _env;

        public ExceptionLoggingMiddleware(RequestDelegate next, ILogger<ExceptionLoggingMiddleware> logger, IWebHostEnvironment env)
        {
            _next = next;
            _logger = logger;
            _env = env;
        }

        public async Task Invoke(HttpContext context)
        {
            var correlationId = context.Request.Headers["X-Correlation-ID"].ToString();
            if (string.IsNullOrEmpty(correlationId))
            {
                correlationId = context.Items["CorrelationId"]?.ToString() ?? context.TraceIdentifier;
            }

            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                var request = context.Request;
                var fullUrl = $"{request.Scheme}://{request.Host}{request.Path}{request.QueryString}";
                var routeData = context.GetRouteData();
                var controller = routeData.Values["controller"]?.ToString() ?? "Unknown";
                var action = routeData.Values["action"]?.ToString() ?? "Unknown";

                var isDev = _env.IsDevelopment();
                int statusCode;
                string title;
                string? field = null;
                string? code = null;

                if (ex is DuplicateBagNumberException duplicateEx)
                {
                    statusCode = (int)HttpStatusCode.Conflict;
                    title = duplicateEx.Message;
                    field = "BagNumber";
                    code = "ERR_DUPLICATE_BAG";
                    _logger.LogWarning("CorrelationId: {CorrelationId} - Duplicate bag number error: {Message}", correlationId, title);
                }
                else if (ex is CosmosException cosmosEx)
                {
                    var raw = cosmosEx.Message;
                    statusCode = (int)cosmosEx.StatusCode;

                    if (raw.Contains("Unique index constraint violation"))
                    {
                        title = "Bag number already exists. Please use a unique BagNumber.";
                        field = "BagNumber";
                        code = "ERR_DUPLICATE_BAG";
                    }
                    else
                    {
                        title = "A database error occurred while processing your request.";
                        code = "ERR_COSMOS_GENERIC";
                    }

                    _logger.LogWarning(cosmosEx, "Cosmos DB error occurred. CorrelationId: {CorrelationId}", correlationId);
                }
                else
                {
                    title = isDev ? "Unhandled exception occurred." : "An unexpected error occurred.";
                    statusCode = (int)HttpStatusCode.InternalServerError;
                    code = "ERR_INTERNAL";
                    _logger.LogError(ex,
                        "Unhandled exception in Controller: {Controller}, Action: {Action}, URI: {Uri}, CorrelationId: {CorrelationId}",
                        controller, action, fullUrl, correlationId);
                }

                var error = new ErrorResponse
                {
                    Title = title,
                    Status = statusCode,
                    CorrelationId = correlationId,
                    Controller = isDev ? controller : null,
                    Action = isDev ? action : null,
                    Path = isDev ? fullUrl : null,
                    Exception = isDev ? ex.Message : null,
                    StackTrace = isDev ? ex.StackTrace : null,
                    Field = field,
                    Code = code
                };

                context.Response.StatusCode = statusCode;

                var acceptHeader = context.Request.Headers["Accept"].ToString().ToLower();
                if (acceptHeader.Contains("application/xml"))
                {
                    context.Response.ContentType = "application/xml";
                    var xmlSerializer = new XmlSerializer(typeof(ErrorResponse));
                    await using var ms = new MemoryStream();
                    xmlSerializer.Serialize(ms, error);
                    ms.Position = 0;
                    await ms.CopyToAsync(context.Response.Body);
                }
                else
                {
                    context.Response.ContentType = "application/json";
                    var json = JsonSerializer.Serialize(error,
                        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                    await context.Response.WriteAsync(json);
                }
            }
        }
    }
}
