using Clustron.Zaris.Samples.Shared;
using Microsoft.Extensions.Caching.Distributed;

namespace Clustron.Zaris.Sample.DistributedCache;

internal class DistributedCacheSampleApp
{
    private readonly IDistributedCache _cache;

    public DistributedCacheSampleApp(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task RunAsync()
    {
        ConsoleHelper.Section("Distributed Cache Sample");

        var context = new SampleContext("distributed");

        ConsoleHelper.Info($"Session Prefix: {context.Prefix}");

        var key = context.Key("user:1");

        // ============================================================
        // SET
        // ============================================================
        ConsoleHelper.Section("SET");

        var value = "John Doe";

        await _cache.SetStringAsync(
            key,
            value,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(20)
            });

        ConsoleHelper.Success($"Stored key: {key}");

        // ============================================================
        // GET
        // ============================================================
        ConsoleHelper.Section("GET");

        var result = await _cache.GetStringAsync(key);

        if (result != null)
        {
            ConsoleHelper.Success("Value retrieved successfully.");
            Console.WriteLine($"Value: {result}");
        }
        else
        {
            ConsoleHelper.Error("Value not found.");
        }

        // ============================================================
        // OVERWRITE
        // ============================================================
        ConsoleHelper.Section("OVERWRITE");

        await _cache.SetStringAsync(key, "Jane Doe");

        var updated = await _cache.GetStringAsync(key);

        ConsoleHelper.Success($"Updated Value: {updated}");

        // ============================================================
        // REMOVE
        // ============================================================
        ConsoleHelper.Section("REMOVE");

        await _cache.RemoveAsync(key);

        var removed = await _cache.GetStringAsync(key);

        Console.WriteLine($"After remove => {(removed == null ? "Not Found" : "Exists")}");

        // ============================================================
        // TTL DEMO
        // ============================================================
        ConsoleHelper.Section("TTL Expiry Demo");

        var ttlKey = context.Key("user:ttl");

        await _cache.SetStringAsync(
            ttlKey,
            "TTL Test",
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(5)
            });

        ConsoleHelper.Info("Waiting 7 seconds...");

        await Task.Delay(TimeSpan.FromSeconds(7));

        var expired = await _cache.GetStringAsync(ttlKey);

        Console.WriteLine($"After TTL => {(expired == null ? "Expired" : "Still Exists")}");

        // ============================================================
        // CLEANUP
        // ============================================================
        ConsoleHelper.Section("Cleanup");

        await _cache.RemoveAsync(key);
        await _cache.RemoveAsync(ttlKey);

        ConsoleHelper.Success("Sample cleanup completed.");
    }
}