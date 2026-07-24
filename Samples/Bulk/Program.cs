using Clustron.Dictus.Client.DependencyInjection;
using Clustron.Dictus.Samples.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Clustron.Dictus.Sample.Bulk;

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
        services.AddSingleton<BulkSampleApp>();
    })
    .Build();


// -----------------------------------------------------
// Run App
// -----------------------------------------------------
var app = host.Services.GetRequiredService<BulkSampleApp>();
return await SampleRunner.RunAsync("Bulk", () => app.RunAsync());