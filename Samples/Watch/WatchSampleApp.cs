using System;
using System.Threading;
using System.Threading.Tasks;
using Clustron.Dictus.Abstractions;
using Clustron.Dictus.Client;
using Clustron.Dictus.Client.Helpers;
using Clustron.Dictus.Samples.Shared;

namespace Clustron.Dictus.Sample.Watch;

internal class WatchSampleApp
{
    private readonly IDictusClientProvider _provider;

    private const string StoreName = "teststore";

    public WatchSampleApp(IDictusClientProvider provider)
    {
        _provider = provider;
    }

    public async Task RunAsync()
    {
        var client = await _provider.GetAsync(StoreName);

        ConsoleHelper.Success($"Connected to store: {StoreName}");

        var context = new SampleContext("watch");

        var dictus = (IDictus)client;
        var watch = dictus.Watch;

        ConsoleHelper.Info($"Session Prefix: {context.Prefix}");

        // ============================================================
        // Setup Keys
        // ============================================================

        var key = context.Key("profile:1");

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
        // Simulate Updates
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

        await Task.Delay(TimeSpan.FromSeconds(6));
        cts.Cancel();

        try { await writerTask; } catch { }

        // ============================================================
        // Stop Watchers
        // ============================================================

        ConsoleHelper.Section("Stopping Watchers");

        await keySub.StopAsync();
        await prefixSub.StopAsync();

        ConsoleHelper.Success("Watchers stopped.");

        // ============================================================
        // Summary
        // ============================================================

        ConsoleHelper.Section("Event Summary");

        Console.WriteLine($"Total KEY events:    {keyEventCount}");
        Console.WriteLine($"Total PREFIX events: {prefixEventCount}");

        // ============================================================
        // Cleanup
        // ============================================================

        ConsoleHelper.Section("Cleanup");

        await client.ClearAsync(new ClearRequest(context.Prefix));

        ConsoleHelper.Success("Cleanup completed.");
    }
}