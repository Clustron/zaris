using Clustron.Zaris.Client.DependencyInjection;
using Clustron.Zaris.Samples.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Clustron.Zaris.Sample.Basic;

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
        services.AddSingleton<BasicSampleApp>();
    })
    .Build();


// -----------------------------------------------------
// Run App
// -----------------------------------------------------
var app = host.Services.GetRequiredService<BasicSampleApp>();
return await SampleRunner.RunAsync("Basic", () => app.RunAsync());