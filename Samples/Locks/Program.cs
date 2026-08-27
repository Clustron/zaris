using Clustron.Zaris.Client.DependencyInjection;
using Clustron.Zaris.Samples.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Clustron.Zaris.Sample.Locks;

ConsoleHelper.Header("Clustron Zaris – Distributed Locks Sample");

// -----------------------------------------------------
// Build Host
// -----------------------------------------------------
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddClustronZarisFromConnectionStrings(context.Configuration);

        services.AddSingleton<LocksSampleApp>();
    })
    .Build();

// -----------------------------------------------------
// Run App
// -----------------------------------------------------
var app = host.Services.GetRequiredService<LocksSampleApp>();
return await SampleRunner.RunAsync("Locks", () => app.RunAsync());
