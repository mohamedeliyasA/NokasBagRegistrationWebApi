using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;

namespace Nokas.CashMgmt.BagRegistration.API.Configuration
{
    public static class AuthConfig
    {
        public static void AddAzureAdJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
        {
            var azureAD = configuration.GetSection("AzureAd");

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.Authority = azureAD["Authority"];
                    options.Audience = azureAD["Audience"];
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = azureAD["ValidIssuer"],
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true
                        // RoleClaimType intentionally left at its default (ClaimTypes.Role).
                        // The JWT handler's default inbound claim mapping already renames the
                        // token's "roles" claim to that same URI, so no override is needed.
                    };

                    options.Events = new JwtBearerEvents
                    {
                        OnAuthenticationFailed = context =>
                        {
                            var correlationId = context.HttpContext?.Items["CorrelationId"]?.ToString() ?? "N/A";
                            Log.Error("CorrelationId: {CorrelationId} - Authentication failed. Invalid token: {Exception}", correlationId, context.Exception.Message);
                            return Task.CompletedTask;
                        }
                    };
                });
        }

        public static void AddApiAuthorizationPolicies(this IServiceCollection services)
        {
            services.AddAuthorization(options =>
            {
                options.AddPolicy("PartnerAccess", policy => policy.RequireRole("cashbag.api.access"));
            });
        }
    }
}
