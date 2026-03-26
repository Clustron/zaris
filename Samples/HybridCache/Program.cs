using Clustron.DKV.Client.DependencyInjection;
using Clustron.DKV.HybridCache;
using Clustron.Dkv.Samples.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Clustron.Dkv.Sample.HybridCache;

ConsoleHelper.Header("Clustron DKV – HybridCache Sample");

// -----------------------------------------------------
// Build Host
// -----------------------------------------------------
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        var stores = context.Configuration.GetSection("Dkv:Stores");

        // -----------------------------------------------------
        // L2 (Remote store from config)
        // -----------------------------------------------------
        services.AddClustronDkvStores(stores);

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
await app.RunAsync();

Console.WriteLine("\nDone.");