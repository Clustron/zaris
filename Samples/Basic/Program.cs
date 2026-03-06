using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Clustron.DKV.Abstractions;
using Clustron.DKV.Client;
using Clustron.Dkv.Samples.Shared;
using Microsoft.Extensions.Configuration;
using Clustron.Dkv.Samples.Shared.Models;

namespace Clustron.Dkv.Sample.Basic
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            ConsoleHelper.Header("Clustron DKV – Basic Sample");

            // Load configuration
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            var options = config
                .GetSection("Dkv")
                .Get<DkvOptions>()
                ?? throw new InvalidOperationException("Missing Dkv configuration section.");

            var mode = options.GetMode();
            IDkvClient client;
            // Initialize client (EXPLICIT API USAGE)
            if (mode == DkvClientMode.Remote)
            {
                if (options.Seeds == null || options.Seeds.Count == 0)
                    throw new InvalidOperationException("No seed servers configured.");

                client = await DKVClient.InitializeRemote(
                    options.ClusterId,
                    options.Seeds,
                    options.LogFilePath);
            }
            else
            {
                client = await DKVClient.InitializeInProc(
                    options.ClusterId,
                    options.LogFilePath);
            }

            ConsoleHelper.Success("Connected to cluster.");
            SampleEnvironmentPrinter.Print(options, mode);

            var restult = await client.ClearAsync();
            // Create sample context (isolates keys)
            var context = new SampleContext("basic");

            ConsoleHelper.Info($"Session Prefix: {context.Prefix}");

            //  PUT
            ConsoleHelper.Section("PUT");

            var key = context.Key("customer:1");

            var customer = new Customer
            {
                Id = Guid.NewGuid(),
                Name = "John Doe",
                Email = "john@demo.com"
            };

            var putResult = await client.PutAsync(
                key,
                customer,
                Put.WithEntity("customer")
                   .WithTtl(TimeSpan.FromSeconds(30))
                   .WithContentType("application/json")
                   .WithLabels(new Dictionary<string, LabelValue>
                   {
                       ["env"] = new LabelValue("demo"),
                       ["sample"] = new LabelValue("basic")
                   })
            );

            if (putResult.IsSuccess)
                ConsoleHelper.Success($"Stored key: {key}");
            else
                ConsoleHelper.Error($"PUT failed: {putResult.Status}");

            //  GET
            ConsoleHelper.Section("GET");

            var getResult = await client.GetAsync<Customer>(key);

            if (getResult.IsSuccess)
            {
                ConsoleHelper.Success("Value retrieved successfully.");

                Console.WriteLine($"Name:  {getResult.Value?.Name}");
                Console.WriteLine($"Email: {getResult.Value?.Email}");

                Console.WriteLine("\nMetadata:");
                Console.WriteLine($"TTL:         {getResult.Metadata?.Ttl}");
                Console.WriteLine($"ContentType: {getResult.Metadata?.ContentType}");
                Console.WriteLine($"CreatedAt:   {getResult.Metadata?.CreatedAt}");

                if (getResult.Metadata?.Labels != null)
                {
                    Console.WriteLine("Labels:");
                    foreach (var label in getResult.Metadata.Labels)
                    {
                        Console.WriteLine($"  {label.Key} = {label.Value.Value}");
                    }
                }
            }
            else
            {
                ConsoleHelper.Error($"GET failed: {getResult.Status}");
            }

            //  COUNTER
            ConsoleHelper.Section("COUNTER");

            var counters = ((IDkv)client).Counters;
            var counterKey = context.Key("orders");

            var increment = await counters.AddAsync(counterKey, 1);

            if (increment.IsSuccess)
                ConsoleHelper.Success($"Counter value: {increment.Value.Current}");

            //  TTL Expiry Demo
            ConsoleHelper.Section("TTL Expiry Demo");
            ConsoleHelper.Info("Waiting 45 seconds...");

            await Task.Delay(TimeSpan.FromSeconds(35));

            var expiredCheck = await client.GetAsync<Customer>(key);
            Console.WriteLine($"After TTL → Status: {expiredCheck.Status}");

            //  Cleanup
            ConsoleHelper.Section("Cleanup");

            await client.ClearAsync(new ClearRequest(context.Prefix));

            ConsoleHelper.Success("Sample cleanup completed.");

            Console.WriteLine("\nDone.");
        }
    }
}
