using Azure.Identity;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using Nokas.CashMgmt.BagRegistration.API.Configuration;
using Nokas.CashMgmt.BagRegistration.API.Middleware;
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
builder.Services.AddHttpClient();
builder.Services.AddScoped<IFunctionInvoker, FunctionInvoker>();
builder.Services.Configure<BlobStorageOptions>(builder.Configuration.GetSection("BlobStorage"));
builder.Services.Configure<CosmosDbOptions>(builder.Configuration.GetSection("CosmosDb"));
builder.Services.Configure<AzureAdOptions>(builder.Configuration.GetSection("AzureAd"));
builder.Services.Configure<KeyVaultOptions>(builder.Configuration.GetSection("KeyVault"));
builder.Services.Configure<ServiceBusOptions>(builder.Configuration.GetSection("ServiceBus"));

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
builder.Services.AddSwaggerDocumentation(builder.Configuration);

builder.Services.AddControllers(options =>
{
    options.ReturnHttpNotAcceptable = true;
})
.AddNewtonsoftJson()
.AddXmlSerializerFormatters();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddAutoMapper(config =>
{
    config.AddMaps(AppDomain.CurrentDomain.GetAssemblies());
});

#endregion

#region Dependency Injection

builder.Services.AddScoped<IBagRegistrationRepo, CosmosBagRegistrationRepo>();
builder.Services.AddSingleton<IBlobArchiveService, BlobArchiveService>();
builder.Services.AddSingleton<IServiceBusSender, ServiceBusSender>();

// Blob archiving: background queue pattern
builder.Services.AddSingleton<IPayloadQueue, InMemoryPayloadQueue>();
builder.Services.AddHostedService<PayloadArchiverHostedService>();
builder.Services.AddHttpClient();

#endregion

var app = builder.Build();

#region Middleware

app.UseMiddleware<ExceptionLoggingMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestArchivingMiddleware>();

app.UseStaticFiles();
app.UseHttpsRedirection();
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

app.Run();
