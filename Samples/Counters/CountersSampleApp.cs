using System;
using System.Threading.Tasks;
using Clustron.Dictus.Abstractions;
using Clustron.Dictus.Client;
using Clustron.Dictus.Samples.Shared;

namespace Clustron.Dictus.Sample.Counters;

internal class CountersSampleApp
{
    private readonly IDictusClientProvider _provider;

    private const string StoreName = "teststore";

    public CountersSampleApp(IDictusClientProvider provider)
    {
        _provider = provider;
    }

    public async Task RunAsync()
    {
        // -----------------------------------------------------
        // Resolve client
        // -----------------------------------------------------
        var client = await _provider.GetAsync(StoreName);

        ConsoleHelper.Success($"Connected to store: {StoreName}");

        await client.ClearAsync();

        var context = new SampleContext("counters");

        ConsoleHelper.Info($"Session Prefix: {context.Prefix}");

        var counters = ((IDictus)client).Counters;

        // ============================================================
        // Atomic Increment
        // ============================================================

        ConsoleHelper.Section("Atomic Increment");

        var counterKey = context.Key("orders");

        var add1 = await counters.AddAsync(counterKey, 5);
        Console.WriteLine($"Added 5 → Previous: {add1.Value.Previous}, Current: {add1.Value.Current}");

        var add2 = await counters.AddAsync(counterKey, 3);
        Console.WriteLine($"Added 3 → Previous: {add2.Value.Previous}, Current: {add2.Value.Current}");

        // ============================================================
        // Get Counter
        // ============================================================

        ConsoleHelper.Section("Get Counter");

        var get = await counters.GetAsync(counterKey);

        if (get.IsSuccess)
            ConsoleHelper.Success($"Counter Value: {get.Value}");
        else
            ConsoleHelper.Error($"GET failed: {get.Status}");

        // ============================================================
        // Set Counter
        // ============================================================

        ConsoleHelper.Section("Set Counter");

        var set = await counters.SetAsync(counterKey, 100);
        Console.WriteLine($"Set to: {set.Value}");

        // ============================================================
        // Bounds (Min / Max)
        // ============================================================

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

        // ============================================================
        // Counter TTL
        // ============================================================

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

        // ============================================================
        // Cleanup
        // ============================================================

        ConsoleHelper.Section("Cleanup");

        await client.ClearAsync(new ClearRequest(context.Prefix));

        ConsoleHelper.Success("Cleanup completed.");
    }
}