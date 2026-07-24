using Clustron.DKV.Client.DependencyInjection;
using Clustron.Dkv.Samples.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Clustron.Dkv.Sample.Search;

ConsoleHelper.Header("Clustron DKV – Search Sample");

// -----------------------------------------------------
// Build Host
// -----------------------------------------------------
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddClustronDkvStores(
            context.Configuration.GetSection("Dkv:Stores"));

        services.AddSingleton<SearchSampleApp>();
    })
    .Build();

// -----------------------------------------------------
// Run
// -----------------------------------------------------
var app = host.Services.GetRequiredService<SearchSampleApp>();
return await SampleRunner.RunAsync("Search", () => app.RunAsync());