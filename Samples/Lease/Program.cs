using Clustron.Dictus.Client.DependencyInjection;
using Clustron.Dictus.Samples.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Clustron.Dictus.Sample.Lease;

ConsoleHelper.Header("Clustron Dictus – Lease Sample (Expiry Validation)");

// -----------------------------------------------------
// Build Host
// -----------------------------------------------------
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddClustronDictusStores(
            context.Configuration.GetSection("Dictus:Stores"));

        services.AddSingleton<LeaseSampleApp>();
    })
    .Build();

// -----------------------------------------------------
// Run
// -----------------------------------------------------
var app = host.Services.GetRequiredService<LeaseSampleApp>();
return await SampleRunner.RunAsync("Lease", () => app.RunAsync());