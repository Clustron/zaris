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

            // 1️⃣ Load configuration
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            var options = config
                .GetSection("Dkv")
                .Get<DkvOptions>()
                ?? throw new InvalidOperationException("Missing Dkv configuration.");

            var mode = options.GetMode();

            // 2️⃣ Initialize client
            var client = await DKVClient.Initialize(
                clusterId: options.ClusterId,
                mode: mode,
                remoteHost: mode == DkvClientMode.Remote ? options.RemoteHost : null,
                remotePort: mode == DkvClientMode.Remote ? options.RemotePort : 0,
                logFilePath: options.LogFilePath
            );

            ConsoleHelper.Success("Connected to cluster.");

            var context = new SampleContext("counters");
            var cleanup = new SampleCleanup();

            ConsoleHelper.Info($"Session Prefix: {context.Prefix}");

            var counters = ((IDkv)client).Counters;

            // 3️⃣ Basic Increment
            ConsoleHelper.Section("Atomic Increment");

            var counterKey = context.Key("orders");
            cleanup.Track(counterKey);

            var add1 = await counters.AddAsync(counterKey, 5);
            Console.WriteLine($"Added 5 → Previous: {add1.Value.Previous}, Current: {add1.Value.Current}");

            var add2 = await counters.AddAsync(counterKey, 3);
            Console.WriteLine($"Added 3 → Previous: {add2.Value.Previous}, Current: {add2.Value.Current}");

            // 4️⃣ Get Current Value
            ConsoleHelper.Section("Get Counter");

            var get = await counters.GetAsync(counterKey);

            if (get.IsSuccess)
                ConsoleHelper.Success($"Counter Value: {get.Value}");
            else
                ConsoleHelper.Error($"GET failed: {get.Status}");

            // 5️⃣ Set Counter
            ConsoleHelper.Section("Set Counter");

            var set = await counters.SetAsync(counterKey, 100);
            Console.WriteLine($"Set to: {set.Value}");

            // 6️⃣ Min / Max Bounds
            ConsoleHelper.Section("Bounds (Min / Max)");

            var boundedKey = context.Key("bounded");
            cleanup.Track(boundedKey);

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

            // 7️⃣ TTL on Counter
            ConsoleHelper.Section("Counter TTL");

            var ttlKey = context.Key("ttl-counter");
            cleanup.Track(ttlKey);

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

            // 8️⃣ Cleanup
            ConsoleHelper.Section("Cleanup");
            await cleanup.CleanupAsync(client);
            ConsoleHelper.Success("Cleanup completed.");

            Console.WriteLine("\nDone.");
        }
    }
}
