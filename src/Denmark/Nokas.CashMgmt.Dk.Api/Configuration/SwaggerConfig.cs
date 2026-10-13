using Microsoft.OpenApi;

namespace Nokas.CashMgmt.Dk.Api.Configuration;

public static class SwaggerConfig
{
    public static void AddSwaggerDocumentation(this IServiceCollection services, IConfiguration configuration)
    {
        var deploymentTier = configuration["Deployment:Tier"] ?? "Unknown";

        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "NOKAS Bag Registration API - Denmark",
                Version = "v1",
                Description =
                    "Click Authorize and paste a bearer token (no 'Bearer ' prefix) to try requests.<br/><br/>" +
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

            // Surfaces the request-body contract from the controller's XML doc comments.
            // The action takes no bound model - validation runs against the raw JSON so that
            // errors name the offending JSON path - so this is where the shape is documented.
            var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                c.IncludeXmlComments(xmlPath);
            }
        });
    }

    // Swagger is reached through Front Door rather than APIM, and the App Service allows the
    // whole AzureFrontDoor.Backend service tag - which is every Front Door instance, not just
    // ours. The FDID header is what narrows that back down to this profile.
    public static void UseSwaggerRestrictions(this IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            return;
        }

        app.Use(async (context, next) =>
        {
            var path = context.Request.Path.Value ?? string.Empty;

            if (!path.StartsWith("/dk/swagger", StringComparison.OrdinalIgnoreCase))
            {
                await next();
                return;
            }

            var config = context.RequestServices.GetRequiredService<IConfiguration>();
            var allowedFdIds = config.GetSection("FrontDoor:AllowedIds").Get<string[]>() ?? [];

            if (allowedFdIds.Length == 0)
            {
                await next();
                return;
            }

            var fdid = context.Request.Headers.TryGetValue("X-Azure-FDID", out var header)
                ? header.ToString()
                : string.Empty;

            if (!allowedFdIds.Any(id => string.Equals(id, fdid, StringComparison.OrdinalIgnoreCase)))
            {
                var logger = context.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("SwaggerRestrictions");

                logger.LogWarning("Swagger access blocked. FDID={FDID}", string.IsNullOrEmpty(fdid) ? "(missing)" : fdid);
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync("Forbidden: Swagger is only accessible via approved Azure Front Door.");
                return;
            }

            await next();
        });
    }
}
