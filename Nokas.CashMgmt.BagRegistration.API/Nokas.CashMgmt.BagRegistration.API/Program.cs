using Azure.Identity;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using Nokas.CashMgmt.BagRegistration.API.Configuration;
using Nokas.CashMgmt.BagRegistration.API.Middleware;
using Nokas.CashMgmt.BagRegistration.API.Models;
using Nokas.CashMgmt.BagRegistration.API.Repository;
using Nokas.CashMgmt.BagRegistration.API.Services;
using Nokas.CashMgmt.BagRegistration.API.Services.Queue;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

#region Logging

builder.Logging.ClearProviders();

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.ApplicationInsights(
            services.GetRequiredService<TelemetryClient>(),
            TelemetryConverter.Traces);

    if (context.HostingEnvironment.IsDevelopment())
    {
        configuration.WriteTo.Console();
    }
});

builder.Services.AddApplicationInsightsTelemetry(options =>
{
    options.EnableAdaptiveSampling = false;
    options.EnableDebugLogger = false;
    options.EnablePerformanceCounterCollectionModule = false;
});

#endregion

#region Key Vault

builder.Host.ConfigureAppConfiguration((context, config) =>
{
    var builtConfig = config.Build();

    var keyVaultUri = builtConfig["KeyVault:Uri"];
    if (!string.IsNullOrEmpty(keyVaultUri))
    {
        config.AddAzureKeyVault(new Uri(keyVaultUri), new DefaultAzureCredential());
    }
});

#endregion

#region Configuration Injection

builder.Services.AddOptions<BlobStorageOptions>()
    .Bind(builder.Configuration.GetSection("BlobStorage"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<CosmosDbOptions>()
    .Bind(builder.Configuration.GetSection("CosmosDb"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<AzureAdOptions>()
    .Bind(builder.Configuration.GetSection("AzureAd"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.Configure<KeyVaultOptions>(builder.Configuration.GetSection("KeyVault"));

builder.Services.AddOptions<ServiceBusOptions>()
    .Bind(builder.Configuration.GetSection("ServiceBus"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<BagStatusFunctionOptions>()
    .Bind(builder.Configuration.GetSection("BagStatusFunction"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddHttpClient<IFunctionInvoker, FunctionInvoker>(client =>
{
    // The resilience handler below owns the timeout budget.
    client.Timeout = Timeout.InfiniteTimeSpan;
})
.AddStandardResilienceHandler(options =>
{
    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(8);
    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(10);
});

#endregion

#region Cosmos DB

builder.Services.AddSingleton(serviceProvider =>
{
    var config = serviceProvider.GetRequiredService<IOptions<CosmosDbOptions>>().Value;
    return new CosmosClient(config.Account, config.KeyValue);
});

#endregion

#region Core Infrastructure

builder.Services.AddAzureAdJwtAuthentication(builder.Configuration);
builder.Services.AddApiAuthorizationPolicies();
builder.Services.AddSwaggerDocumentation(builder.Configuration);

builder.Services.AddControllers(options =>
{
    options.ReturnHttpNotAcceptable = true;
})
.AddNewtonsoftJson()
.AddXmlSerializerFormatters();

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var correlationId = context.HttpContext.Items["CorrelationId"]?.ToString() ?? "N/A";
        var errorMessages = context.ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage)
            .ToList();

        var error = new ErrorResponse
        {
            Title = "Validation Failed",
            Status = StatusCodes.Status400BadRequest,
            CorrelationId = correlationId,
            Exception = string.Join(" | ", errorMessages)
        };

        return new BadRequestObjectResult(error);
    };
});

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddAutoMapper(config =>
{
    config.AddMaps(AppDomain.CurrentDomain.GetAssemblies());
});

builder.Services.AddHealthChecks();

#endregion

#region Dependency Injection

builder.Services.AddScoped<IBagRegistrationRepo, CosmosBagRegistrationRepo>();
builder.Services.AddSingleton<IBlobArchiveService, BlobArchiveService>();
builder.Services.AddSingleton<IServiceBusSender, ServiceBusSender>();

// Blob archiving: background queue pattern
builder.Services.AddSingleton<IPayloadQueue, InMemoryPayloadQueue>();
builder.Services.AddHostedService<PayloadArchiverHostedService>();

// Outbox dispatcher: reliably publishes Service Bus events from Cosmos-committed state
builder.Services.AddHostedService<OutboxDispatcherHostedService>();

#endregion

var app = builder.Build();

#region Middleware

app.UseHttpsRedirection();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionLoggingMiddleware>();
app.UseMiddleware<RequestArchivingMiddleware>();

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

#endregion

#region Swagger

app.UseSwaggerRestrictions(app.Environment);
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "NOKAS Bag Registration API");
});

#endregion

app.MapControllers();
app.MapHealthChecks("/healthz");

app.Run();
