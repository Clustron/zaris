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

        // -----------------------------------------------------
        // L2 (Remote store from config)
        // -----------------------------------------------------
        services.AddClustronZarisFromConnectionStrings(context.Configuration);

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