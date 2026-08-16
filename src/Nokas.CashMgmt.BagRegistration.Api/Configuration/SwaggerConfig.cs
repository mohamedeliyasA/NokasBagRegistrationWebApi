using Microsoft.OpenApi;

namespace Nokas.CashMgmt.BagRegistration.Api.Configuration;

public static class SwaggerConfig
{
    public static void AddSwaggerDocumentation(this IServiceCollection services, IConfiguration configuration)
    {
        var environmentName = configuration["ASPNETCORE_ENVIRONMENT"] ?? "Unknown";

        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "NOKAS Bag Registration API (v2)",
                Version = "v1",
                Description =
                    "Secure bag registration API. Click Authorize and paste a bearer token (no 'Bearer ' prefix) to try requests.<br/><br/>" +
                    $"<small>Environment: {environmentName} &nbsp;|&nbsp; Build: {BuildInfo.Stamp}</small>",
                Contact = new OpenApiContact
                {
                    Name = "Nokas Support",
                    Email = "BizTalkOps@nokas.no"
                }
            });

            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "Enter your Azure AD access token below (no 'Bearer ' prefix).",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "Bearer",
                BearerFormat = "JWT"
            });

            c.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecuritySchemeReference("Bearer", null),
                    new List<string>()
                }
            });
        });
    }

    // Left off deliberately while v2 wasn't reachable through Front Door (everything would
    // have 403'd since no request ever carried X-Azure-FDID). Now that APIM/Front Door route
    // here, this needs to be active in Production the same way the old codebase does it.
    public static void UseSwaggerRestrictions(this IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (!env.IsProduction())
            return;

        app.Use(async (context, next) =>
        {
            var logger = context.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("SwaggerRestrictions");

            var config = context.RequestServices.GetRequiredService<IConfiguration>();
            var allowedFdIds = config.GetSection("FrontDoor:AllowedIds").Get<string[]>() ?? [];

            var path = context.Request.Path.Value ?? string.Empty;
            var method = context.Request.Method;
            var fdidHeader = context.Request.Headers.TryGetValue("X-Azure-FDID", out var fdid) ? fdid.ToString() : "(missing)";

            var isSwaggerPath =
                path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("/swagger/", StringComparison.OrdinalIgnoreCase);

            logger.LogInformation(
                "Request to {Path} (Method={Method}), FDID={FDID}, IsSwagger={IsSwagger}, Allowed={Allowed}",
                path, method, fdidHeader, isSwaggerPath, string.Join(",", allowedFdIds));

            if (isSwaggerPath && allowedFdIds.Length > 0)
            {
                if (string.IsNullOrEmpty(fdidHeader) ||
                    !allowedFdIds.Any(id => string.Equals(id, fdidHeader, StringComparison.OrdinalIgnoreCase)))
                {
                    logger.LogWarning("Swagger access blocked. FDID={FDID}, Allowed={Allowed}", fdidHeader, string.Join(",", allowedFdIds));
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsync("Forbidden: Swagger is only accessible via approved Azure Front Door.");
                    return;
                }
            }

            var referer = context.Request.Headers.Referer.ToString();
            var isSwaggerReferer = referer.Contains("/swagger", StringComparison.OrdinalIgnoreCase);
            var isModifying = HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsDelete(method);

            if (isSwaggerReferer && isModifying)
            {
                logger.LogWarning("Swagger UI attempted modifying operation. Method={Method}", method);
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync("Swagger UI is restricted from modifying data in production. Use API clients like Postman instead.");
                return;
            }

            await next();
        });
    }
}
