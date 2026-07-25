using Clustron.Zaris.Client.DependencyInjection;
using Clustron.Zaris.Samples.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Clustron.Zaris.Sample.LeaderElection;

ConsoleHelper.Header("Clustron Zaris – Leader Election via Lease");

// -----------------------------------------------------
// Build Host
// -----------------------------------------------------
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddClustronZarisStores(
            context.Configuration.GetSection("Zaris:Stores"));

        services.AddSingleton<LeaderElectionSampleApp>();
    })
    .Build();

// -----------------------------------------------------
// Run
// -----------------------------------------------------
var app = host.Services.GetRequiredService<LeaderElectionSampleApp>();
return await SampleRunner.RunAsync("LeaderElection", () => app.RunAsync());