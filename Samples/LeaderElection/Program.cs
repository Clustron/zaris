using Clustron.DKV.Client.DependencyInjection;
using Clustron.Dkv.Samples.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Clustron.Dkv.Sample.LeaderElection;

ConsoleHelper.Header("Clustron DKV – Leader Election via Lease");

// -----------------------------------------------------
// Build Host
// -----------------------------------------------------
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddClustronDkvStores(
            context.Configuration.GetSection("Dkv:Stores"));

        services.AddSingleton<LeaderElectionSampleApp>();
    })
    .Build();

// -----------------------------------------------------
// Run
// -----------------------------------------------------
var app = host.Services.GetRequiredService<LeaderElectionSampleApp>();
return await SampleRunner.RunAsync("LeaderElection", () => app.RunAsync());