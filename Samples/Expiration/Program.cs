using Clustron.Zaris.Client.DependencyInjection;
using Clustron.Zaris.Samples.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Clustron.Zaris.Sample.Expiration;

ConsoleHelper.Header("Clustron Zaris – Expiration (TTL) Sample");

// -----------------------------------------------------
// Build Host
// -----------------------------------------------------
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddClustronZarisFromConnectionStrings(context.Configuration);

        services.AddSingleton<ExpirationSampleApp>();
    })
    .Build();

// -----------------------------------------------------
// Run App
// -----------------------------------------------------
var app = host.Services.GetRequiredService<ExpirationSampleApp>();
return await SampleRunner.RunAsync("Expiration", () => app.RunAsync());
