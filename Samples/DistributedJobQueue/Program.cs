using Clustron.Zaris.Client.DependencyInjection;
using Clustron.Zaris.Samples.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Clustron.Zaris.Sample.SimpleEnterpriseQueue;

ConsoleHelper.Header("Clustron Zaris - Simplified Enterprise Job Queue");

// -----------------------------------------------------
// Build Host
// -----------------------------------------------------
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddClustronZarisStores(
            context.Configuration.GetSection("Zaris:Stores"));

        services.AddSingleton<SimpleQueueSampleApp>();
    })
    .Build();

// -----------------------------------------------------
// Run App
// -----------------------------------------------------
var app = host.Services.GetRequiredService<SimpleQueueSampleApp>();
return await SampleRunner.RunAsync("DistributedJobQueue", () => app.RunAsync());