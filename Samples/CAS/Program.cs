using Clustron.Dictus.Client.DependencyInjection;
using Clustron.Dictus.Samples.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Clustron.Dictus.Sample.CAS;

ConsoleHelper.Header("Clustron Dictus – Compare-And-Swap (CAS) Sample");

// -----------------------------------------------------
// Build Host
// -----------------------------------------------------
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddClustronDictusStores(
            context.Configuration.GetSection("Dictus:Stores"));

        services.AddSingleton<CasSampleApp>();
    })
    .Build();

// -----------------------------------------------------
// Run App
// -----------------------------------------------------
var app = host.Services.GetRequiredService<CasSampleApp>();
return await SampleRunner.RunAsync("CAS", () => app.RunAsync());