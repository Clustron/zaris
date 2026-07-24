using Clustron.Dictus.Client.DependencyInjection;
using Clustron.Dictus.Samples.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Clustron.Dictus.Sample.Counters;

ConsoleHelper.Header("Clustron Dictus – Counters Sample");

// -----------------------------------------------------
// Build Host
// -----------------------------------------------------
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddClustronDictusStores(
            context.Configuration.GetSection("Dictus:Stores"));

        services.AddSingleton<CountersSampleApp>();
    })
    .Build();

// -----------------------------------------------------
// Run App
// -----------------------------------------------------
var app = host.Services.GetRequiredService<CountersSampleApp>();
return await SampleRunner.RunAsync("Counters", () => app.RunAsync());