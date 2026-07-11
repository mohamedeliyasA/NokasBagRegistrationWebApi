using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nokas.CashMgmt.BagReg.Func;


var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureAppConfiguration((ctx, cfg) =>
    {
    })
    .ConfigureServices((ctx, services) =>
    {
        var config = ctx.Configuration;

        // KeyVault SecretClient (uses Managed Identity in Azure; DefaultAzureCredential locally)
        //var kvName = config["KEYVAULT_NAME"];
        //if (!string.IsNullOrEmpty(kvName))
        //{
        //    var kvUri = new Uri($"https://{kvName}.vault.azure.net/");
        //    services.AddSingleton(new SecretClient(kvUri, new DefaultAzureCredential()));
        //}

        // Register minimal bag status service
        services.AddScoped<IBagStatusService, BagStatusService>();
    })
    .Build();

await host.RunAsync();
