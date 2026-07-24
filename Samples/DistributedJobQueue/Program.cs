using Clustron.DKV.Client.DependencyInjection;
using Clustron.Dkv.Samples.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Clustron.Dkv.Sample.SimpleEnterpriseQueue;

ConsoleHelper.Header("Clustron DKV - Simplified Enterprise Job Queue");

// -----------------------------------------------------
// Build Host
// -----------------------------------------------------
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddClustronDkvStores(
            context.Configuration.GetSection("Dkv:Stores"));

        services.AddSingleton<SimpleQueueSampleApp>();
    })
    .Build();

// -----------------------------------------------------
// Run App
// -----------------------------------------------------
var app = host.Services.GetRequiredService<SimpleQueueSampleApp>();
return await SampleRunner.RunAsync("DistributedJobQueue", () => app.RunAsync());