using Azure.Core;
using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nokas.CashMgmt.Dk.Func.Services;
using Nokas.CashMgmt.Dk.Shared.Configuration;
using Nokas.CashMgmt.Dk.Shared.Repositories;
using Nokas.CashMgmt.Dk.Shared.Services;
using Nokas.CashMgmt.Common.Infrastructure;

var builder = FunctionsApplication.CreateBuilder(args);

var azureCredential = Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT") == "Development"
    ? (TokenCredential)new AzureCliCredential()
    : new DefaultAzureCredential();

var keyVaultUri = builder.Configuration["KeyVault:Uri"];
if (!string.IsNullOrEmpty(keyVaultUri))
{
    builder.Configuration.AddAzureKeyVault(new Uri(keyVaultUri), azureCredential);
}

builder.Services.AddOptions<DkCosmosOptions>()
    .Bind(builder.Configuration.GetSection("CosmosDb"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<ServiceNowOptions>()
    .Bind(builder.Configuration.GetSection("ServiceNow"));

// Held in a wrapper so "no credential" is a valid state: a missing secret must fail the sync
// loudly on each run, not stop the host from starting.
builder.Services.AddSingleton(new ServiceNowCredentialHolder(
    ServiceNowCredential.TryParse(builder.Configuration["ServiceNow:Dk:Credential"])));

builder.Services.AddSingleton(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<DkCosmosOptions>>().Value;

    // Without this the SDK's own serializer ignores [JsonPropertyName] and writes PascalCase,
    // including "Id", which Cosmos then cannot use as the document id.
    return new CosmosClient(options.AccountEndpoint, azureCredential, new CosmosClientOptions
    {
        Serializer = new SystemTextJsonCosmosSerializer(),
        ConnectionMode = ConnectionMode.Direct
    });
});

builder.Services.AddMemoryCache();
builder.Services.AddHttpClient(ServiceNowHttp.ClientName, client =>
{
    client.Timeout = TimeSpan.FromSeconds(builder.Configuration.GetValue("ServiceNow:TimeoutSeconds", 30));
});

builder.Services.AddSingleton(serviceProvider => new ServiceNowClock(
    serviceProvider.GetRequiredService<IOptions<ServiceNowOptions>>().Value.CountingResultSync.TimeZone,
    serviceProvider.GetRequiredService<ILogger<ServiceNowClock>>()));

builder.Services.AddSingleton<IServiceNowCountingResultClient, ServiceNowCountingResultClient>();
builder.Services.AddSingleton<IDkCountingResultRepository, CosmosDkCountingResultRepository>();
builder.Services.AddSingleton<IDkKardexMapRepository, CosmosDkKardexMapRepository>();
builder.Services.AddSingleton<DkCountingResultSyncService>();

builder.Services.AddApplicationInsightsTelemetryWorkerService();
builder.Services.ConfigureFunctionsApplicationInsights();

builder.Build().Run();
