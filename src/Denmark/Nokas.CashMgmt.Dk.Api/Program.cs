using System.Text.Json.Serialization;
using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using Nokas.CashMgmt.Dk.Api.Configuration;
using Nokas.CashMgmt.Dk.Api.Middleware;
using Nokas.CashMgmt.Dk.Api.Repositories;
using Nokas.CashMgmt.Dk.Shared.Configuration;
using Nokas.CashMgmt.Dk.Shared.Repositories;
using Nokas.CashMgmt.Dk.Api.Services;
using Nokas.CashMgmt.Common.Correlation;
using Nokas.CashMgmt.Common.Errors;
using Nokas.CashMgmt.Common.Infrastructure;
using Serilog;
using Serilog.Events;
using Nokas.CashMgmt.Dk.Shared.Services;

var builder = WebApplication.CreateBuilder(args);

// Managed Identity only exists inside an actual Azure resource - probing for it locally is
// slow and can hard-fail instead of falling through. Use the CLI login directly in
// Development; deployed environments keep the full chain.
var azureCredential = builder.Environment.IsDevelopment()
    ? (TokenCredential)new AzureCliCredential()
    : new DefaultAzureCredential();

#region Logging

builder.Logging.ClearProviders();

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.ApplicationInsights(
            services.GetRequiredService<Microsoft.ApplicationInsights.TelemetryClient>(),
            TelemetryConverter.Traces,
            restrictedToMinimumLevel: LogEventLevel.Warning);

    if (context.HostingEnvironment.IsDevelopment())
    {
        configuration.WriteTo.Console();
    }
});

builder.Services.AddApplicationInsightsTelemetry(options =>
{
    options.EnableAdaptiveSampling = false;
});

#endregion

#region Key Vault

var keyVaultUri = builder.Configuration["KeyVault:Uri"];
if (!string.IsNullOrEmpty(keyVaultUri))
{
    builder.Configuration.AddAzureKeyVault(new Uri(keyVaultUri), azureCredential);
}

#endregion

#region Options

builder.Services.AddOptions<CosmosDbOptions>()
    .Bind(builder.Configuration.GetSection("CosmosDb"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<BlobStorageOptions>()
    .Bind(builder.Configuration.GetSection("BlobStorage"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<AzureAdOptions>()
    .Bind(builder.Configuration.GetSection("AzureAd"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<StubOptions>()
    .Bind(builder.Configuration.GetSection("Stub"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<ServiceNowOptions>()
    .Bind(builder.Configuration.GetSection("ServiceNow"));

// The counting-result and kardex repositories live in Dk.Shared and take only the Cosmos
// coordinates they need; the sync Function binds the same keys.
builder.Services.AddOptions<DkCosmosOptions>()
    .Bind(builder.Configuration.GetSection("CosmosDb"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<DkCountingResultsOptions>()
    .Bind(builder.Configuration.GetSection("CountingResults"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Parsed once at startup from the Key Vault secret. Held in a wrapper so that "no credential"
// is a valid state: a missing or malformed secret makes registrations fail with a clear
// 503, but never stops the app from starting or serving counting results.
builder.Services.AddSingleton(new ServiceNowCredentialHolder(
    ServiceNowCredential.TryParse(builder.Configuration["ServiceNow:Dk:Credential"])));

#endregion

#region Azure clients

builder.Services.AddSingleton(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<CosmosDbOptions>>().Value;

    var clientOptions = new CosmosClientOptions
    {
        // Without this the SDK's own serializer ignores [JsonPropertyName] and writes
        // PascalCase, including "Id", which Cosmos then cannot use as the document id.
        Serializer = new SystemTextJsonCosmosSerializer(),
        ConnectionMode = ConnectionMode.Direct
    };

    return new CosmosClient(options.AccountEndpoint, azureCredential, clientOptions);
});

builder.Services.AddSingleton(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<BlobStorageOptions>>().Value;
    return new BlobServiceClient(new Uri(options.AccountUri), azureCredential);
});

#endregion

#region Services

builder.Services.AddApiAuthentication(builder.Configuration);
builder.Services.AddApiAuthorizationPolicies(builder.Configuration);
builder.Services.AddSwaggerDocumentation(builder.Configuration);

builder.Services.AddControllers(options =>
{
    options.ReturnHttpNotAcceptable = true;
})
.AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
})
.AddXmlSerializerFormatters();

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var correlationId = context.HttpContext.Items[CorrelationConstants.HttpContextItemsKey]?.ToString() ?? "N/A";
        var errorMessages = context.ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage)
            .ToList();

        var error = new ErrorResponse
        {
            Title = "Validation Failed",
            Status = StatusCodes.Status400BadRequest,
            CorrelationId = correlationId,
            Code = "VALIDATION_ERROR",
            Exception = string.Join(" | ", errorMessages)
        };

        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogWarning("CorrelationId: {CorrelationId} - Model validation failed: {Errors}",
            correlationId, error.Exception);

        return new BadRequestObjectResult(error);
    };
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHealthChecks();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IDkBagRegistrationRepository, CosmosDkBagRegistrationRepository>();
builder.Services.AddScoped<IDkCustomerDirectoryRepository, CosmosDkCustomerDirectoryRepository>();
builder.Services.AddScoped<IDkKardexMapRepository, CosmosDkKardexMapRepository>();
builder.Services.AddScoped<IDkCountingResultRepository, CosmosDkCountingResultRepository>();
builder.Services.AddDataProtection();
builder.Services.AddSingleton<IContinuationTokenService, ContinuationTokenService>();
builder.Services.AddSingleton<IPayloadArchiveQueue, InMemoryPayloadArchiveQueue>();
builder.Services.AddHostedService<PayloadArchiverBackgroundService>();
builder.Services.AddScoped<IServiceNowRegistrationClient, ServiceNowRegistrationClient>();

// No retry handler on purpose: a retried POST that had already succeeded would create a
// duplicate registration in ServiceNow. Retrying is the partner's decision, keyed on messageId.
builder.Services.AddHttpClient(ServiceNowHttp.ClientName, client =>
{
    var seconds = builder.Configuration.GetValue("ServiceNow:TimeoutSeconds", 10);
    client.Timeout = TimeSpan.FromSeconds(seconds);
});

#endregion

var app = builder.Build();

#region Middleware

app.UseHttpsRedirection();

// Surfaces the deployed build on every response so a deployment can be verified from the
// outside.
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        context.Response.Headers["X-Build-Stamp"] = BuildInfo.Stamp;
        return Task.CompletedTask;
    });

    await next();
});

app.UseMiddleware<CorrelationMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseAuthentication();
app.UseMiddleware<ClientIdMiddleware>();
app.UseAuthorization();

// Deliberately after authorization: only payloads from a caller who cleared the role check
// are archived, so a rejected request cannot write to Blob storage.
app.UseMiddleware<PayloadArchivingMiddleware>();

#endregion

// Served under /dk/swagger rather than /swagger so Front Door can route it to this app
// with a plain path match: /swagger/* on the shared TEST domain already belongs to the
// Norwegian API, and a rewrite rule would be needed otherwise.
app.UseSwaggerRestrictions(app.Environment);
app.UseSwagger(c => c.RouteTemplate = "dk/swagger/{documentName}/swagger.json");
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/dk/swagger/v1/swagger.json", "NOKAS Bag Registration API - Denmark");
    c.RoutePrefix = "dk/swagger";
});

app.MapControllers();
app.MapHealthChecks("/healthz").AllowAnonymous();

app.Run();
