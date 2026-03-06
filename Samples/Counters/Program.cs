using System;
using System.Threading.Tasks;
using Clustron.DKV.Abstractions;
using Clustron.DKV.Client;
using Clustron.Dkv.Samples.Shared;
using Microsoft.Extensions.Configuration;

namespace Clustron.Dkv.Sample.Shared.Counters
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            ConsoleHelper.Header("Clustron DKV – Counters Sample");

            //  Load configuration
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            var options = config
                .GetSection("Dkv")
                .Get<DkvOptions>()
                ?? throw new InvalidOperationException("Missing Dkv configuration.");

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

            var context = new SampleContext("counters");
            
            ConsoleHelper.Info($"Session Prefix: {context.Prefix}");

            var counters = ((IDkv)client).Counters;

            // Basic Increment
            ConsoleHelper.Section("Atomic Increment");

            var counterKey = context.Key("orders");

            var add1 = await counters.AddAsync(counterKey, 5);
            Console.WriteLine($"Added 5 → Previous: {add1.Value.Previous}, Current: {add1.Value.Current}");

            var add2 = await counters.AddAsync(counterKey, 3);
            Console.WriteLine($"Added 3 → Previous: {add2.Value.Previous}, Current: {add2.Value.Current}");

            //  Get Current Value
            ConsoleHelper.Section("Get Counter");

            var get = await counters.GetAsync(counterKey);

            if (get.IsSuccess)
                ConsoleHelper.Success($"Counter Value: {get.Value}");
            else
                ConsoleHelper.Error($"GET failed: {get.Status}");

            //  Set Counter
            ConsoleHelper.Section("Set Counter");

            var set = await counters.SetAsync(counterKey, 100);
            Console.WriteLine($"Set to: {set.Value}");

            //  Min / Max Bounds
            ConsoleHelper.Section("Bounds (Min / Max)");

            var boundedKey = context.Key("bounded");

            var first = await counters.AddAsync(
                boundedKey,
                10,
                new CounterOptions { MaxValue = 10 });

            Console.WriteLine($"Initial Add → {first.Value.Current}");

            var exceed = await counters.AddAsync(
                boundedKey,
                1,
                new CounterOptions { MaxValue = 10 });

            Console.WriteLine($"Exceed Max → Status: {exceed.Status}");

            //  TTL on Counter
            ConsoleHelper.Section("Counter TTL");

            var ttlKey = context.Key("ttl-counter");

            var ttlAdd = await counters.AddAsync(
                ttlKey,
                7,
                new CounterOptions
                {
                    Ttl = TimeSpan.FromSeconds(20)
                });

            Console.WriteLine($"TTL Counter Created → Current: {ttlAdd.Value.Current}");

            ConsoleHelper.Info("Waiting 25 seconds...");
            await Task.Delay(TimeSpan.FromSeconds(25));

            var expired = await counters.GetAsync(ttlKey);
            Console.WriteLine($"After TTL → Status: {expired.Status}");

            //  Cleanup
            ConsoleHelper.Section("Cleanup"); 
            await client.ClearAsync(new ClearRequest(context.Prefix));

            ConsoleHelper.Success("Cleanup completed.");

            Console.WriteLine("\nDone.");
        }
    }
}
