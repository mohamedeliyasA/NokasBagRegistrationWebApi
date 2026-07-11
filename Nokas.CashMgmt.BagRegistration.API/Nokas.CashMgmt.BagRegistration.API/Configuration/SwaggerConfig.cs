using System.Reflection;
using Microsoft.OpenApi.Models;
using Nokas.CashMgmt.BagRegistration.API.Models;

namespace Nokas.CashMgmt.BagRegistration.API.Configuration
{
    public static class SwaggerConfig
    {
        public static void AddSwaggerDocumentation(this IServiceCollection services, IConfiguration configuration)
        {
            var postmanUrl = configuration["Swagger:PostmanCollectionUrl"];

            var postmanLinkHtml = !string.IsNullOrWhiteSpace(postmanUrl)
                ? $@"<a href=""{postmanUrl}"" target=""_blank"">📥 Download Postman Collection</a>"
                : @"<em>Postman collection URL is not configured.</em>";

            services.AddSwaggerGen(c =>
            {
                // ---- Core doc info ----
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "NOKAS Bag Registration API",
                    Version = "v1",
                    Description =
$@"Secure bag registration API.<br/>
{postmanLinkHtml}<br/><br/>
To access and try the API methods, click the <strong>Authorize</strong> button below and provide a valid token.",
                    Contact = new OpenApiContact
                    {
                        Name = "Nokas Support",
                        Email = "BizTalkOps@nokas.no"
                    }
                });

                // ---- Security ----
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description =
@" Enter your Azure AD access token below.

To get a token, send this POST request to Azure AD:

POST &lt;your-Token URL&gt;

With form fields:
- grant_type=client_credentials
- client_id=&lt;your-clientid&gt;
- client_secret=&lt;your-secret&gt;
- scope=&lt;your-scope&gt;

Paste the token below (no 'Bearer ' prefix).",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.Http,
                    Scheme = "Bearer",
                    BearerFormat = "JWT"
                });

                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });

                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                c.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
                c.SchemaFilter<HideInternalFieldsSchemaFilter>();
                c.MapType<BagRegistrationRequest>(() => new OpenApiSchema { Type = "object" });
            });
        }

        public static void UseSwaggerRestrictions(this IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (!env.IsProduction())
                return;

            app.Use(async (context, next) =>
            {
                var logger = context.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("SwaggerRestrictions");

                var config = app.ApplicationServices.GetRequiredService<IConfiguration>();
                var allowedFdIds = config.GetSection("FrontDoor:AllowedIds").Get<string[]>() ?? Array.Empty<string>();

                var path = context.Request.Path.Value ?? string.Empty;
                var method = context.Request.Method;
                var fdidHeader = context.Request.Headers.TryGetValue("X-Azure-FDID", out var fdid) ? fdid.ToString() : "(missing)";

                bool isSwaggerPath =
                        path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase) ||
                        path.Contains("/swagger/", StringComparison.OrdinalIgnoreCase) ||
                        path.StartsWith("/docs/bagreg/swagger", StringComparison.OrdinalIgnoreCase);

                logger.LogInformation(
                    "Request to {Path} (Method={Method}), FDID={FDID}, IsSwagger={IsSwagger}, Allowed={Allowed}",
                    path, method, fdidHeader, isSwaggerPath, string.Join(",", allowedFdIds));

                // --- Check for AFD ---
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

                // --- Keep your existing modify-block ---
                var referer = context.Request.Headers["Referer"].ToString();
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
}
