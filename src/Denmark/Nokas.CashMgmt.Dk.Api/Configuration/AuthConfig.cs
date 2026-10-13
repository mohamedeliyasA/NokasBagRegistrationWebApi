using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Nokas.CashMgmt.Dk.Api.Configuration;

public static class AuthConfig
{
    public const string RegistrationRolePolicy = "DkPartnerRegistrationAccess";
    public const string CountingResultRolePolicy = "DkPartnerCountingResultRead";

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
                    // This tenant issues app-only tokens with the v1 issuer even though the
                    // v2.0 authority is used to request them, so OIDC discovery's expected
                    // issuer does not match. Pin it explicitly.
                    ValidIssuer = $"https://sts.windows.net/{azureAd.TenantId}/",
                    ValidateAudience = true,
                    // The audience is configured with a trailing slash in places and issued
                    // without one; accept both rather than depending on which side normalises.
                    ValidAudiences = [azureAd.Audience, azureAd.Audience.TrimEnd('/')]
                    // Do not set RoleClaimType - the default inbound claim mapping already
                    // maps the token's "roles" claim to ClaimTypes.Role.
                };
            });
    }

    public static void AddApiAuthorizationPolicies(this IServiceCollection services, IConfiguration configuration)
    {
        var requiredRole = configuration["AzureAd:RequiredRole"]
            ?? throw new InvalidOperationException("AzureAd:RequiredRole is not configured.");

        // Separate role from registration, same pattern as Norway's PartnerRegistrationAccess /
        // PartnerCountingResultRead split - a partner using only one surface is not granted the
        // other. Creating/granting this role in Entra is the owner's own manual step, never
        // done by automation here; the app only reads whatever role name is configured.
        var countingResultRole = configuration["AzureAd:CountingResultRequiredRole"]
            ?? throw new InvalidOperationException("AzureAd:CountingResultRequiredRole is not configured.");

        services.AddAuthorization(options =>
        {
            options.AddPolicy(RegistrationRolePolicy, policy => policy.RequireRole(requiredRole));
            options.AddPolicy(CountingResultRolePolicy, policy => policy.RequireRole(countingResultRole));
        });
    }
}
