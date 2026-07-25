using System;
using System.Linq;
using System.Threading.Tasks;
using Clustron.Zaris.Abstractions;
using Clustron.Zaris.Client;
using Clustron.Zaris.Client.Helpers;
using Clustron.Zaris.Samples.Shared;

namespace Clustron.Zaris.Sample.Lease;

internal class LeaseSampleApp
{
    private readonly IZarisClientProvider _provider;

    private const string StoreName = "teststore";

    public LeaseSampleApp(IZarisClientProvider provider)
    {
        _provider = provider;
    }

    public async Task RunAsync()
    {
        var client = await _provider.GetAsync(StoreName);

        ConsoleHelper.Success($"Connected to store: {StoreName}");

        var context = new SampleContext("lease");

        // Start clean
        await client.ClearAsync(new ClearRequest(context.Prefix));

        var zaris = (IZaris)client;
        var leases = zaris.Leases;
        var watch = zaris.Watch;

        // ============================================================
        // Grant Lease & Attach Keys
        // ============================================================

        ConsoleHelper.Section("Grant Lease (10 seconds)");

        var lease = await leases.GrantAsync(TimeSpan.FromSeconds(10));
        if (!lease.IsSuccess)
        {
            ConsoleHelper.Error("Lease grant failed.");
            return;
        }

        ConsoleHelper.Success($"Lease Granted: {lease.Value}");

        var keys = Enumerable.Range(1, 5)
            .Select(i => context.Key($"lease-key-{i}"))
            .ToList();

        foreach (var key in keys)
        {
            await client.PutAsync(key, $"value-{key}", Put.WithLease(lease.Value));
        }

        ConsoleHelper.Info($"Inserted {keys.Count} keys bound to lease.");
        Console.WriteLine($"Count After Insert: {client.Count}");

        ConsoleHelper.Info("Attaching WATCH to first key...");

        var (sub, _) = await watch.WatchKeyAsync(
            keys.First(),
            null,
            ev =>
            {
                Console.WriteLine($"WATCH EVENT: {ev.EventType} for {ev.Key}");
            });

        // ============================================================
        // Let Lease Expire
        // ============================================================

        ConsoleHelper.Section("Waiting For Lease Expiry");
        ConsoleHelper.Info("Waiting 15 seconds for lease to expire...");

        await Task.Delay(TimeSpan.FromSeconds(15));

        ConsoleHelper.Section("Post-Expiry Verification");

        foreach (var key in keys)
        {
            var result = await client.GetAsync<string>(key);
            Console.WriteLine($"Key: {key} => Status: {result.Status}");
        }

        Console.WriteLine($"Count After Expiry: {client.Count}");

        await sub.StopAsync();

        // ============================================================
        // Compare With Explicit Revoke
        // ============================================================

        ConsoleHelper.Section("Revoke Comparison Test");

        var lease2 = await leases.GrantAsync(TimeSpan.FromSeconds(30));
        if (!lease2.IsSuccess)
        {
            ConsoleHelper.Error("Second lease grant failed.");
            return;
        }

        ConsoleHelper.Success($"Lease2 Granted: {lease2.Value}");

        var revokeKeys = Enumerable.Range(1, 3)
            .Select(i => context.Key($"revoke-key-{i}"))
            .ToList();

        foreach (var k in revokeKeys)
            await client.PutAsync(k, "revoke", Put.WithLease(lease2.Value));

        Console.WriteLine($"Before Revoke => Count: {client.Count}");

        await leases.RevokeAsync(lease2.Value);

        ConsoleHelper.Info("After Revoke:");

        foreach (var key in revokeKeys)
        {
            var result = await client.GetAsync<string>(key);
            Console.WriteLine($"Key: {key} → Status: {result.Status}");
        }

        Console.WriteLine($"Count After Revoke: {client.Count}");

        ConsoleHelper.Success("Lease sample completed.");

        // ============================================================
        // CLEANUP
        // ============================================================

        ConsoleHelper.Section("Cleanup");

        await client.ClearAsync(new ClearRequest(context.Prefix));

        ConsoleHelper.Success("Sample cleanup completed.");
    }
}