using Clustron.Zaris.Client.DependencyInjection;
using Clustron.Zaris.Samples.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Clustron.Zaris.Sample.Hashes;

ConsoleHelper.Header("Clustron Zaris – Hashes Sample");

// -----------------------------------------------------
// Build Host
// -----------------------------------------------------
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddClustronZarisFromConnectionStrings(context.Configuration);

        services.AddSingleton<HashesSampleApp>();
    })
    .Build();

// -----------------------------------------------------
// Run App
// -----------------------------------------------------
var app = host.Services.GetRequiredService<HashesSampleApp>();
return await SampleRunner.RunAsync("Hashes", () => app.RunAsync());
