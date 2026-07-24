using Clustron.DKV.Client.DependencyInjection;
using Clustron.Dkv.Samples.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Clustron.Dkv.Sample.Lease;

ConsoleHelper.Header("Clustron DKV – Lease Sample (Expiry Validation)");

// -----------------------------------------------------
// Build Host
// -----------------------------------------------------
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddClustronDkvStores(
            context.Configuration.GetSection("Dkv:Stores"));

        services.AddSingleton<LeaseSampleApp>();
    })
    .Build();

// -----------------------------------------------------
// Run
// -----------------------------------------------------
var app = host.Services.GetRequiredService<LeaseSampleApp>();
return await SampleRunner.RunAsync("Lease", () => app.RunAsync());