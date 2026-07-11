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
    }
}
