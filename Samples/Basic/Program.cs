using Clustron.DKV.Client.DependencyInjection;
using Clustron.Dkv.Samples.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Clustron.Dkv.Sample.Basic;

ConsoleHelper.Header("Clustron DKV – Basic Sample");

// -----------------------------------------------------
// Build Host
// -----------------------------------------------------
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        // -----------------------------------------------------
        // Register DKV (from config)
        // -----------------------------------------------------
        services.AddClustronDkvStores(
            context.Configuration.GetSection("Dkv:Stores"));


        // App
        services.AddSingleton<BasicSampleApp>();
    })
    .Build();


// -----------------------------------------------------
// Run App
// -----------------------------------------------------
var app = host.Services.GetRequiredService<BasicSampleApp>();
return await SampleRunner.RunAsync("Basic", () => app.RunAsync());