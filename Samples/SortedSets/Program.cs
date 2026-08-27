using Clustron.Zaris.Client.DependencyInjection;
using Clustron.Zaris.Samples.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Clustron.Zaris.Sample.SortedSets;

ConsoleHelper.Header("Clustron Zaris – Sorted Sets Sample");

// -----------------------------------------------------
// Build Host
// -----------------------------------------------------
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddClustronZarisFromConnectionStrings(context.Configuration);

        services.AddSingleton<SortedSetsSampleApp>();
    })
    .Build();

// -----------------------------------------------------
// Run App
// -----------------------------------------------------
var app = host.Services.GetRequiredService<SortedSetsSampleApp>();
return await SampleRunner.RunAsync("SortedSets", () => app.RunAsync());
