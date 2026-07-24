using Clustron.DKV.Client.DependencyInjection;
using Clustron.Dkv.Samples.Shared;
using Clustron.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Clustron.Dkv.Sample.DistributedCache;

ConsoleHelper.Header("Clustron DKV – DistributedCache Sample");

// -----------------------------------------------------
// Build Host
// -----------------------------------------------------
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        var stores = context.Configuration.GetSection("Dkv:Stores");

        // -----------------------------------------------------
        // Register DKV (from config)
        // -----------------------------------------------------
        services.AddClustronDkvStores(stores);

        // -----------------------------------------------------
        // Register Distributed Cache
        // -----------------------------------------------------
        services.AddClustronDistributedCache("teststore", opt =>
        {
            opt.KeyPrefix = "sample:";
        });

        // App
        services.AddSingleton<DistributedCacheSampleApp>();
    })
    .Build();

// -----------------------------------------------------
// Run
// -----------------------------------------------------
var app = host.Services.GetRequiredService<DistributedCacheSampleApp>();
return await SampleRunner.RunAsync("IDistributedCache", () => app.RunAsync());