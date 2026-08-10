using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nokas.CashMgmt.BagRegistration.Func.Configuration;
using Nokas.CashMgmt.BagRegistration.Func.Services;
using Nokas.CashMgmt.Shared.Infrastructure;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

var cosmosOptions = builder.Configuration.GetSection("CosmosDb").Get<CosmosDbOptions>()
    ?? throw new InvalidOperationException("CosmosDb configuration section is missing.");
builder.Services.AddSingleton(cosmosOptions);

builder.Services.AddSingleton(_ => new CosmosClient(
    cosmosOptions.AccountEndpoint,
    (Azure.Core.TokenCredential)new DefaultAzureCredential(),
    new CosmosClientOptions { Serializer = new SystemTextJsonCosmosSerializer() }));

builder.Services.AddSingleton<IBagStatusService, BagStatusService>();
builder.Services.AddSingleton<IBagNumberCacheSyncService, BagNumberCacheSyncService>();

builder.Build().Run();