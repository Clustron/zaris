using Clustron.Zaris.Client.DependencyInjection;
using Clustron.Zaris.Samples.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Clustron.Zaris.Sample.Watch;

ConsoleHelper.Header("Clustron Zaris – Watch Sample");

// -----------------------------------------------------
// Build Host
// -----------------------------------------------------
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddClustronZarisStores(
            context.Configuration.GetSection("Zaris:Stores"));

        services.AddSingleton<WatchSampleApp>();
    })
    .Build();

// -----------------------------------------------------
// Run
// -----------------------------------------------------
var app = host.Services.GetRequiredService<WatchSampleApp>();
return await SampleRunner.RunAsync("Watch", () => app.RunAsync());