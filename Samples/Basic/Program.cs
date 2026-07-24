using Clustron.Dictus.Client.DependencyInjection;
using Clustron.Dictus.Samples.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Clustron.Dictus.Sample.Basic;

ConsoleHelper.Header("Clustron Dictus – Basic Sample");

// -----------------------------------------------------
// Build Host
// -----------------------------------------------------
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        // -----------------------------------------------------
        // Register Dictus (from config)
        // -----------------------------------------------------
        services.AddClustronDictusStores(
            context.Configuration.GetSection("Dictus:Stores"));


        // App
        services.AddSingleton<BasicSampleApp>();
    })
    .Build();


// -----------------------------------------------------
// Run App
// -----------------------------------------------------
var app = host.Services.GetRequiredService<BasicSampleApp>();
return await SampleRunner.RunAsync("Basic", () => app.RunAsync());