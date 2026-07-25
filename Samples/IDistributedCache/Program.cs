using Clustron.Zaris.Client.DependencyInjection;
using Clustron.Zaris.Samples.Shared;
using Clustron.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Clustron.Zaris.Sample.DistributedCache;

ConsoleHelper.Header("Clustron Zaris – DistributedCache Sample");

// -----------------------------------------------------
// Build Host
// -----------------------------------------------------
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        var stores = context.Configuration.GetSection("Zaris:Stores");

        // -----------------------------------------------------
        // Register Zaris (from config)
        // -----------------------------------------------------
        services.AddClustronZarisStores(stores);

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