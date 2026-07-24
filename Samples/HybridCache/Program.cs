using Clustron.Dictus.Client.DependencyInjection;
using Clustron.Dictus.HybridCache;
using Clustron.Dictus.Samples.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Clustron.Dictus.Sample.HybridCache;

ConsoleHelper.Header("Clustron Dictus – HybridCache Sample");

// -----------------------------------------------------
// Build Host
// -----------------------------------------------------
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        var stores = context.Configuration.GetSection("Dictus:Stores");

        // -----------------------------------------------------
        // L2 (Remote store from config)
        // -----------------------------------------------------
        services.AddClustronDictusStores(stores);

        // -----------------------------------------------------
        // Hybrid Cache (L1 + L2)
        // -----------------------------------------------------
        services.AddClustronHybridCache("l1store", "teststore");

        // App
        services.AddSingleton<HybridCacheSampleApp>();
    })
    .Build();

// -----------------------------------------------------
// Run
// -----------------------------------------------------
var app = host.Services.GetRequiredService<HybridCacheSampleApp>();
return await SampleRunner.RunAsync("HybridCache", () => app.RunAsync());