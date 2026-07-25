using Clustron.Zaris.Client.DependencyInjection;
using Clustron.Zaris.Samples.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Clustron.Zaris.Sample.CAS;

ConsoleHelper.Header("Clustron Zaris – Compare-And-Swap (CAS) Sample");

// -----------------------------------------------------
// Build Host
// -----------------------------------------------------
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddClustronZarisStores(
            context.Configuration.GetSection("Zaris:Stores"));

        services.AddSingleton<CasSampleApp>();
    })
    .Build();

// -----------------------------------------------------
// Run App
// -----------------------------------------------------
var app = host.Services.GetRequiredService<CasSampleApp>();
return await SampleRunner.RunAsync("CAS", () => app.RunAsync());