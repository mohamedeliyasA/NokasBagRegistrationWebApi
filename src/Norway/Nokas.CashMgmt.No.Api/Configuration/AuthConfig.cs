using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Nokas.CashMgmt.No.Api.Configuration;

public static class AuthConfig
{
    public const string RegistrationRolePolicy = "PartnerRegistrationAccess";
    public const string RegistrationRoleName = "cashbag.registration.access";

    public const string CountingResultRolePolicy = "PartnerCountingResultRead";
    public const string CountingResultRoleName = "cashbag.countingresult.read";

    public static void AddApiAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var azureAd = configuration.GetSection("AzureAd").Get<AzureAdOptions>()
            ?? throw new InvalidOperationException("AzureAd configuration section is missing.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                                options.Authority = azureAd.Authority;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = $"https://sts.windows.net/{azureAd.TenantId}/",
                    ValidateAudience = true,
                    ValidAudience = azureAd.Audience
                    // Do not set RoleClaimType — the default inbound claim mapping already
                    // maps the token's "roles" claim to ClaimTypes.Role. Overriding it breaks
                    // role-based authorization.
                };

            });
    }

    public static void AddApiAuthorizationPolicies(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(RegistrationRolePolicy, policy =>
                policy.RequireRole(RegistrationRoleName));
            options.AddPolicy(CountingResultRolePolicy, policy =>
                policy.RequireRole(CountingResultRoleName));
        });
    }
}