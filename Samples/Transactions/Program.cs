using Clustron.DKV.Client.DependencyInjection;
using Clustron.Dkv.Samples.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Clustron.Dkv.Sample.TransactionsBasic;

ConsoleHelper.Header("Clustron DKV – Basic Transaction Sample");

// -----------------------------------------------------
// Build Host
// -----------------------------------------------------
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddClustronDkvStores(
            context.Configuration.GetSection("Dkv:Stores"));

        services.AddSingleton<TransactionsBasicSampleApp>();
    })
    .Build();

// -----------------------------------------------------
// Run
// -----------------------------------------------------
var app = host.Services.GetRequiredService<TransactionsBasicSampleApp>();
return await SampleRunner.RunAsync("Transactions", () => app.RunAsync());