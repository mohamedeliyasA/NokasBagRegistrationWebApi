using Azure.Core;
using Azure.Identity;
using Microsoft.ApplicationInsights.Extensibility;
using Nokas.CashMgmt.BagRegistration.Api.Configuration;
using Serilog;
using Serilog.Events;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using Nokas.CashMgmt.Shared.Infrastructure;
using Azure.Storage.Blobs;
using Azure.Messaging.ServiceBus;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Nokas.CashMgmt.Shared.Errors;
using Nokas.CashMgmt.BagRegistration.Api.Repositories;
using Nokas.CashMgmt.BagRegistration.Api.Services;
using Microsoft.ApplicationInsights;

var builder = WebApplication.CreateBuilder(args);

// Managed Identity only exists inside an actual Azure resource - probing for it locally
// is slow and can hard-fail instead of falling through. Use the CLI login directly in
// Development; deployed environments keep the full chain, which resolves to their
// real Managed Identity.
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
            services.GetRequiredService<TelemetryClient>(),
            TelemetryConverter.Traces,
            restrictedToMinimumLevel: LogEventLevel.Warning);

    if (context.HostingEnvironment.IsDevelopment())
    {
        configuration.WriteTo.Console();
        configuration.WriteTo.Seq("http://localhost:5341");
    }
});

builder.Services.AddApplicationInsightsTelemetry(options =>
{
    options.EnableAdaptiveSampling = false;
});

#endregion

#region Key Vault

builder.Host.ConfigureAppConfiguration((context, config) =>
{
    var builtConfig = config.Build();
    var keyVaultUri = builtConfig["KeyVault:Uri"];
    if (!string.IsNullOrEmpty(keyVaultUri))
    {
        config.AddAzureKeyVault(new Uri(keyVaultUri), azureCredential);
    }
});

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

builder.Services.AddOptions<ServiceBusOptions>()
    .Bind(builder.Configuration.GetSection("ServiceBus"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<BagStatusFunctionOptions>()
    .Bind(builder.Configuration.GetSection("BagStatusFunction"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<AzureAdOptions>()
    .Bind(builder.Configuration.GetSection("AzureAd"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<CountingResultsOptions>()
    .Bind(builder.Configuration.GetSection("CountingResults"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

#endregion

#region Cosmos DB

builder.Services.AddSingleton(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<CosmosDbOptions>>().Value;

    var clientOptions = new CosmosClientOptions
    {
        Serializer = new SystemTextJsonCosmosSerializer(),
        ConnectionMode = ConnectionMode.Direct
    };

    return new CosmosClient(options.AccountEndpoint, azureCredential, clientOptions);
});

#endregion

#region Blob Storage

builder.Services.AddSingleton(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<BlobStorageOptions>>().Value;
    return new BlobServiceClient(new Uri(options.AccountUri), azureCredential);
});

#endregion

#region Service Bus

builder.Services.AddSingleton(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<ServiceBusOptions>>().Value;
    return new ServiceBusClient(options.FullyQualifiedNamespace, azureCredential);
});

#endregion

#region builder services
builder.Services.AddApiAuthentication(builder.Configuration);
builder.Services.AddApiAuthorizationPolicies();
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
        var correlationId = context.HttpContext.Items[Nokas.CashMgmt.Shared.Correlation.CorrelationConstants.HttpContextItemsKey]?.ToString() ?? "N/A";
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
builder.Services.AddScoped<IBagRegistrationRepository, CosmosBagRegistrationRepository>();
builder.Services.AddScoped<IBagNumberCacheRepository, CosmosBagNumberCacheRepository>();
builder.Services.AddScoped<ICountingResultRepository, CosmosCountingResultRepository>();
builder.Services.AddDataProtection();
builder.Services.AddSingleton<IContinuationTokenService, ContinuationTokenService>();
builder.Services.AddSingleton<IPayloadArchiveQueue, InMemoryPayloadArchiveQueue>();
builder.Services.AddHostedService<PayloadArchiverBackgroundService>();
builder.Services.AddHostedService<OutboxDispatcherBackgroundService>();
builder.Services.AddHttpClient<IBagStatusFunctionClient, BagStatusFunctionClient>(client =>
{
    client.Timeout = Timeout.InfiniteTimeSpan;
})
.AddStandardResilienceHandler(resilience =>
{
    resilience.AttemptTimeout.Timeout = TimeSpan.FromSeconds(8);
    resilience.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(10);
});

#endregion

var app = builder.Build();

#region Middleware

app.UseHttpsRedirection();

// Surfaces the deployed build on every response so a deployment can be verified
// from the outside (the App Service itself only accepts traffic from APIM/Front Door).
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        context.Response.Headers["X-Build-Stamp"] = BuildInfo.Stamp;
        return Task.CompletedTask;
    });

    await next();
});

app.UseMiddleware<Nokas.CashMgmt.BagRegistration.Api.Middleware.CorrelationMiddleware>();
app.UseMiddleware<Nokas.CashMgmt.BagRegistration.Api.Middleware.ExceptionHandlingMiddleware>();
app.UseMiddleware<Nokas.CashMgmt.BagRegistration.Api.Middleware.PayloadArchivingMiddleware>();

app.UseAuthentication();
app.UseMiddleware<Nokas.CashMgmt.BagRegistration.Api.Middleware.ClientIdMiddleware>();
app.UseAuthorization();

#endregion

#region Swagger

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "NOKAS Bag Registration API (v2)");
});

#endregion

app.MapControllers();
app.MapHealthChecks("/healthz");

app.Run();

