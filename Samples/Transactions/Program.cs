using Clustron.Dictus.Client.DependencyInjection;
using Clustron.Dictus.Samples.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Clustron.Dictus.Sample.TransactionsBasic;

ConsoleHelper.Header("Clustron Dictus – Basic Transaction Sample");

// -----------------------------------------------------
// Build Host
// -----------------------------------------------------
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddClustronDictusStores(
            context.Configuration.GetSection("Dictus:Stores"));

        services.AddSingleton<TransactionsBasicSampleApp>();
    })
    .Build();

// -----------------------------------------------------
// Run
// -----------------------------------------------------
var app = host.Services.GetRequiredService<TransactionsBasicSampleApp>();
return await SampleRunner.RunAsync("Transactions", () => app.RunAsync());