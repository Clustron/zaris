using System;
using System.Threading;
using System.Threading.Tasks;
using Clustron.DKV.Abstractions;
using Clustron.DKV.Client;
using Clustron.Dkv.Samples.Shared;
using Microsoft.Extensions.Configuration;
using Clustron.DKV.Client.Helpers;

namespace Clustron.Dkv.Sample.Watch
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            ConsoleHelper.Header("Clustron DKV – Watch Sample");

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

            var context = new SampleContext("watch");

            var watch = ((IDkv)client).Watch;

            ConsoleHelper.Info($"Session Prefix: {context.Prefix}");

            // ============================================================
            //  Setup Keys
            // ============================================================

            var key = context.Key("profile:1");

            // FIXED: prefix must include session prefix
            var prefix = context.Key("orders:");

            var order1 = prefix + "1";
            var order2 = prefix + "2";

            // ============================================================
            // Event Counters
            // ============================================================

            int keyEventCount = 0;
            int prefixEventCount = 0;

            // ============================================================
            // Start Watchers
            // ============================================================

            ConsoleHelper.Section("Starting Watchers");

            var (keySub, snapshot) = await watch.WatchKeyAsync(
                key,
                new WatchOptions { IncludeInitialSnapshot = true },
                ev =>
                {
                    Interlocked.Increment(ref keyEventCount);

                    Console.WriteLine(
                        $"[KEY EVENT] {ev.EventType} | {ev.Key} | Rev={ev.Revision} | Val={ev.Value}");
                });

            if (snapshot != null)
                ConsoleHelper.Info($"Snapshot Value: {snapshot.GetValue<string>()}");

            var prefixSub = await watch.WatchPrefixAsync(
                prefix,
                new WatchOptions(),
                ev =>
                {
                    Interlocked.Increment(ref prefixEventCount);

                    Console.WriteLine(
                        $"[PREFIX EVENT] {ev.EventType} | {ev.Key} | Rev={ev.Revision} | Val={ev.Value}");
                });

            ConsoleHelper.Success("Watchers started.");

            // ============================================================
            //  Background Writer Simulation
            // ============================================================

            ConsoleHelper.Section("Simulating Live Updates");

            using var cts = new CancellationTokenSource();
            var token = cts.Token;

            var writerTask = Task.Run(async () =>
            {
                int counter = 0;

                while (!token.IsCancellationRequested)
                {
                    counter++;

                    await client.PutAsync(key, $"profile-update-{counter}");
                    await client.PutAsync(order1, $"order1-update-{counter}");
                    await client.PutAsync(order2, $"order2-update-{counter}");

                    if (counter % 3 == 0)
                    {
                        await client.DeleteAsync(key);
                        await client.DeleteAsync(order1);
                    }

                    await Task.Delay(800, token);
                }
            }, token);

            // Let it run for 6 seconds
            await Task.Delay(TimeSpan.FromSeconds(6));
            cts.Cancel();

            try { await writerTask; } catch { }

            // ============================================================
            //  Stop Watchers
            // ============================================================

            ConsoleHelper.Section("Stopping Watchers");

            await keySub.StopAsync();
            await prefixSub.StopAsync();

            ConsoleHelper.Success("Watchers stopped.");

            // ============================================================
            //  Event Summary
            // ============================================================

            ConsoleHelper.Section("Event Summary");

            Console.WriteLine($"Total KEY events:    {keyEventCount}");
            Console.WriteLine($"Total PREFIX events: {prefixEventCount}");

            // ============================================================
            //  Cleanup
            // ============================================================

            ConsoleHelper.Section("Cleanup");

            await client.ClearAsync(new ClearRequest(context.Prefix));

            ConsoleHelper.Success("Cleanup completed.");

            Console.WriteLine("\nDone.");
        }
    }
}