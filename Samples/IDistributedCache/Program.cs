using Clustron.Dictus.Client.DependencyInjection;
using Clustron.Dictus.Samples.Shared;
using Clustron.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Clustron.Dictus.Sample.DistributedCache;

ConsoleHelper.Header("Clustron Dictus – DistributedCache Sample");

// -----------------------------------------------------
// Build Host
// -----------------------------------------------------
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        var stores = context.Configuration.GetSection("Dictus:Stores");

        // -----------------------------------------------------
        // Register Dictus (from config)
        // -----------------------------------------------------
        services.AddClustronDictusStores(stores);

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