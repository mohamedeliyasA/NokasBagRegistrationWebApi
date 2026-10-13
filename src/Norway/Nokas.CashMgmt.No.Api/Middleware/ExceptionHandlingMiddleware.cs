using System.Net;
using System.Text.Json;
using System.Xml.Serialization;
using Microsoft.Azure.Cosmos;
using Nokas.CashMgmt.Common.Correlation;
using Nokas.CashMgmt.Common.Errors;

namespace Nokas.CashMgmt.No.Api.Middleware;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, IWebHostEnvironment env)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            var correlationId = context.Items[CorrelationConstants.HttpContextItemsKey]?.ToString() ?? context.TraceIdentifier;
            var isDev = env.IsDevelopment();

            int statusCode;
            string title;
            string code;

            if (ex is CosmosException cosmosEx)
            {
                // CosmosException.Message can be null for some batch failures.
                var raw = cosmosEx.Message ?? string.Empty;
                statusCode = (int)cosmosEx.StatusCode;

                if (raw.Contains("Unique index constraint violation"))
                {
                    title = "Bag number already exists. Please use a unique BagNumber.";
                    code = "ERR_DUPLICATE_BAG";
                    logger.LogWarning(cosmosEx, "CorrelationId: {CorrelationId} - Cosmos unique index violation.", correlationId);
                }
                else
                {
                    title = "A database error occurred while processing your request.";
                    code = "ERR_COSMOS_GENERIC";
                    logger.LogCritical(cosmosEx, "CorrelationId: {CorrelationId} - Cosmos DB failure.", correlationId);
                }
            }
            else
            {
                title = isDev ? ex.Message : "An unexpected error occurred.";
                statusCode = (int)HttpStatusCode.InternalServerError;
                code = "ERR_INTERNAL";
                logger.LogCritical(ex, "CorrelationId: {CorrelationId} - Unhandled exception.", correlationId);
            }

            var error = new ErrorResponse
            {
                Title = title,
                Status = statusCode,
                CorrelationId = correlationId,
                Code = code,
                Exception = isDev ? ex.Message : null,
                StackTrace = isDev ? ex.StackTrace : null
            };

            context.Response.StatusCode = statusCode;

            var acceptsXml = context.Request.Headers.Accept.ToString().Contains("application/xml", StringComparison.OrdinalIgnoreCase);
            if (acceptsXml)
            {
                context.Response.ContentType = "application/xml";
                var serializer = new XmlSerializer(typeof(ErrorResponse));
                await using var ms = new MemoryStream();
                serializer.Serialize(ms, error);
                ms.Position = 0;
                await ms.CopyToAsync(context.Response.Body);
            }
            else
            {
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonSerializer.Serialize(error,
                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
            }
        }
    }
}