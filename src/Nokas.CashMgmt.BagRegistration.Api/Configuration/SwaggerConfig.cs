using Microsoft.OpenApi;

namespace Nokas.CashMgmt.BagRegistration.Api.Configuration;

public static class SwaggerConfig
{
    public static void AddSwaggerDocumentation(this IServiceCollection services)
    {
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "NOKAS Bag Registration API (v2)",
                Version = "v1",
                Description = "Secure bag registration API. Click Authorize and paste a bearer token (no 'Bearer ' prefix) to try requests.",
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
}
