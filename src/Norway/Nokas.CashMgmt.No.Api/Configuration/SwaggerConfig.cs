using Microsoft.OpenApi;

namespace Nokas.CashMgmt.No.Api.Configuration;

public static class SwaggerConfig
{
    public static void AddSwaggerDocumentation(this IServiceCollection services, IConfiguration configuration)
    {
        // Deliberately not ASPNETCORE_ENVIRONMENT - that stays "Production" on the TEST deployment
        // too, since it gates the Swagger FDID restriction below. This is a separate, purely
        // cosmetic label for which deployment tier a viewer is actually looking at.
        var deploymentTier = configuration["Deployment:Tier"] ?? "Unknown";

        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "NOKAS Bag Registration API (v2)",
                Version = "v1",
                Description =
                    "Secure bag registration API. Click Authorize and paste a bearer token (no 'Bearer ' prefix) to try requests.<br/><br/>" +
                    $"<small>Environment: {deploymentTier} &nbsp;|&nbsp; Build: {BuildInfo.Stamp}</small>",
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

    // FDID check and the modifying-operation block are independent: FDID gates who can reach
    // Swagger at all (active in every deployed environment), while the modifying-operation
    // block follows Deployment:Tier specifically (Test allows Try-it-out, Production blocks
    // it) - they used to share one IsProduction() gate, which meant TEST silently inherited
    // the Production-only modifying-operation block too, since TEST also runs
    // ASPNETCORE_ENVIRONMENT=Production.
    public static void UseSwaggerRestrictions(this IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
            return;

        app.Use(async (context, next) =>
        {
            var logger = context.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("SwaggerRestrictions");

            var config = context.RequestServices.GetRequiredService<IConfiguration>();
            var allowedFdIds = config.GetSection("FrontDoor:AllowedIds").Get<string[]>() ?? [];
            var isProductionTier = string.Equals(config["Deployment:Tier"], "Production", StringComparison.OrdinalIgnoreCase);

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

            if (isSwaggerReferer && isModifying && isProductionTier)
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
