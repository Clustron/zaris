using Clustron.Dictus.HybridCache;
using Clustron.Dictus.Samples.Shared;
using Microsoft.Extensions.Caching.Hybrid;

namespace Clustron.Dictus.Sample.HybridCache;

internal class HybridCacheSampleApp
{
    private readonly ClustronHybridCache _cache;

    public HybridCacheSampleApp(ClustronHybridCache cache)
    {
        _cache = cache;
    }

    public async Task RunAsync()
    {
        ConsoleHelper.Section("Hybrid Cache Sample");
        ConsoleHelper.Info("Hybrid cache = L1 (in-memory) + L2 (distributed)");

        var context = new SampleContext("hybrid");

        ConsoleHelper.Info($"Session Prefix: {context.Prefix}");

        var key = context.Key("product:1");

        var options = new HybridCacheEntryOptions
        {
            Expiration = TimeSpan.FromSeconds(10),         // L2
            LocalCacheExpiration = TimeSpan.FromSeconds(3) // L1
        };

        // ============================================================
        // FIRST CALL → FACTORY
        // ============================================================

        ConsoleHelper.Section("GetOrCreate (Factory Expected)");

        var value = await _cache.GetOrCreateAsync(
            key,
            state: 0,
            async (_, _) =>
            {
                ConsoleHelper.Info("Factory executed (data fetched from source)");
                await Task.Delay(100);
                return "Laptop";
            },
            options: options,
            tags: new[] { "products", "electronics" }
        );

        ConsoleHelper.Success($"Value: {value}");

        // ============================================================
        // L1 HIT
        // ============================================================

        ConsoleHelper.Section("Get (Expected: L1 Hit)");

        bool l1FactoryExecuted = false;

        var l1 = await _cache.GetOrCreateAsync(
            key,
            state: 0,
            async (_, _) =>
            {
                l1FactoryExecuted = true;
                return "INVALID";
            });

        if (l1FactoryExecuted)
            ConsoleHelper.Error("Factory executed → NOT an L1 hit");
        else
            ConsoleHelper.Success("L1 hit confirmed");

        Console.WriteLine($"Value: {l1}");

        // ============================================================
        // L2 HIT (after L1 expiry)
        // ============================================================

        ConsoleHelper.Section("Waiting for L1 expiry...");
        await Task.Delay(options.LocalCacheExpiration!.Value + TimeSpan.FromSeconds(1));

        ConsoleHelper.Section("Get (Expected: L2 Hit)");

        bool l2FactoryExecuted = false;

        var l2 = await _cache.GetOrCreateAsync(
            key,
            state: 0,
            async (_, _) =>
            {
                l2FactoryExecuted = true;
                return "INVALID";
            });

        if (l2FactoryExecuted)
            ConsoleHelper.Error("Factory executed → NOT a cache hit");
        else
            ConsoleHelper.Success("Cache hit confirmed (likely L2)");

        Console.WriteLine($"Value: {l2}");

        // ============================================================
        // REMOVE
        // ============================================================

        ConsoleHelper.Section("Remove");

        await _cache.RemoveAsync(key);

        var afterRemove = await _cache.GetOrCreateAsync(
            key,
            state: 0,
            async (_, _) =>
            {
                ConsoleHelper.Info("Factory executed again after remove");
                return "Laptop v2";
            });

        ConsoleHelper.Success($"After Remove Value: {afterRemove}");

        // ============================================================
        // TAG INVALIDATION
        // ============================================================

        ConsoleHelper.Section("Tag Invalidation");

        var tagKey = context.Key("product:tagged");

        await _cache.GetOrCreateAsync(
            tagKey,
            state: 0,
            async (_, _) => "Phone",
            tags: new[] { "products" });

        ConsoleHelper.Info("Invalidating tag 'products'...");

        await _cache.RemoveByTagAsync("products");

        var afterTagInvalidation = await _cache.GetOrCreateAsync(
            tagKey,
            state: 0,
            async (_, _) =>
            {
                ConsoleHelper.Info("Factory executed after tag invalidation");
                return "Phone v2";
            });

        ConsoleHelper.Success($"After Tag Invalidation: {afterTagInvalidation}");

        // ============================================================
        // TTL DEMO
        // ============================================================

        ConsoleHelper.Section("TTL Demo");

        var ttlKey = context.Key("product:ttl");

        await _cache.SetAsync(
            ttlKey,
            "Tablet",
            options: new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromSeconds(5),
                LocalCacheExpiration = TimeSpan.FromSeconds(2)
            });

        ConsoleHelper.Info("Waiting 7 seconds...");
        await Task.Delay(TimeSpan.FromSeconds(7));

        var ttlCheck = await _cache.GetOrCreateAsync(
            ttlKey,
            state: 0,
            async (_, _) =>
            {
                ConsoleHelper.Info("Factory executed after TTL expiry");
                return "Tablet v2";
            });

        ConsoleHelper.Success($"After TTL: {ttlCheck}");

        // ============================================================
        // CLEANUP
        // ============================================================

        ConsoleHelper.Section("Cleanup");

        await _cache.RemoveAsync(key);
        await _cache.RemoveAsync(tagKey);
        await _cache.RemoveAsync(ttlKey);

        ConsoleHelper.Success("Sample cleanup completed.");
    }
}