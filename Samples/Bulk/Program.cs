using Clustron.Zaris.Client.DependencyInjection;
using Clustron.Zaris.Samples.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Clustron.Zaris.Sample.Bulk;

ConsoleHelper.Header("Clustron Zaris – Basic Sample");

// -----------------------------------------------------
// Build Host
// -----------------------------------------------------
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        // -----------------------------------------------------
        // Register Zaris (from config)
        // -----------------------------------------------------
        services.AddClustronZarisFromConnectionStrings(context.Configuration);


        // App
        services.AddSingleton<BulkSampleApp>();
    })
    .Build();


// -----------------------------------------------------
// Run App
// -----------------------------------------------------
var app = host.Services.GetRequiredService<BulkSampleApp>();
return await SampleRunner.RunAsync("Bulk", () => app.RunAsync());