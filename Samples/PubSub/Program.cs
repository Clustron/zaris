using Clustron.Zaris.Client.DependencyInjection;
using Clustron.Zaris.Samples.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Clustron.Zaris.Sample.PubSub;

ConsoleHelper.Header("Clustron Zaris – Pub/Sub Sample");

// -----------------------------------------------------
// Build Host
// -----------------------------------------------------
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddClustronZarisFromConnectionStrings(context.Configuration);

        services.AddSingleton<PubSubSampleApp>();
    })
    .Build();

// -----------------------------------------------------
// Run App
// -----------------------------------------------------
var app = host.Services.GetRequiredService<PubSubSampleApp>();
return await SampleRunner.RunAsync("PubSub", () => app.RunAsync());
