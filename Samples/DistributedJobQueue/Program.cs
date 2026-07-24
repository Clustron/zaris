using Clustron.Dictus.Client.DependencyInjection;
using Clustron.Dictus.Samples.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Clustron.Dictus.Sample.SimpleEnterpriseQueue;

ConsoleHelper.Header("Clustron Dictus - Simplified Enterprise Job Queue");

// -----------------------------------------------------
// Build Host
// -----------------------------------------------------
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddClustronDictusStores(
            context.Configuration.GetSection("Dictus:Stores"));

        services.AddSingleton<SimpleQueueSampleApp>();
    })
    .Build();

// -----------------------------------------------------
// Run App
// -----------------------------------------------------
var app = host.Services.GetRequiredService<SimpleQueueSampleApp>();
return await SampleRunner.RunAsync("DistributedJobQueue", () => app.RunAsync());