using Clustron.Zaris.Client.DependencyInjection;
using Clustron.Zaris.HybridCache;
using Clustron.Zaris.Samples.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Clustron.Zaris.Sample.HybridCache;

ConsoleHelper.Header("Clustron Zaris – HybridCache Sample");

// -----------------------------------------------------
// Build Host
// -----------------------------------------------------
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        var stores = context.Configuration.GetSection("Zaris:Stores");

        // -----------------------------------------------------
        // L2 (Remote store from config)
        // -----------------------------------------------------
        services.AddClustronZarisStores(stores);

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