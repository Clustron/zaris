using System;
using System.Threading.Tasks;
using Clustron.Zaris.Abstractions;
using Clustron.Zaris.Client;
using Clustron.Zaris.Samples.Shared;

namespace Clustron.Zaris.Sample.Expiration;

/// <summary>
/// Demonstrates TTL management on the core client: set a time-to-live on an existing key
/// (<c>ExpireAsync</c>), read the remaining TTL (<c>GetTimeToLiveAsync</c>), make a key
/// permanent again (<c>PersistAsync</c>), and observe a key disappear once its TTL elapses.
/// These are all on the base <see cref="IZarisClient"/> — no cast to <c>IZaris</c> needed.
/// </summary>
internal class ExpirationSampleApp
{
    private readonly IZarisClientProvider _provider;

    private const string StoreName = "teststore";

    public ExpirationSampleApp(IZarisClientProvider provider)
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

        var context = new SampleContext("expiration");

        ConsoleHelper.Info($"Session Prefix: {context.Prefix}");

        var key = context.Key("session");

        // ============================================================
        // A freshly-written key has no TTL (it is persistent)
        // ============================================================

        ConsoleHelper.Section("Write a persistent key");

        await client.PutAsync(key, "session-payload");
        ConsoleHelper.Info($"TTL after PUT → {Describe(await Ttl(client, key))}");

        // ============================================================
        // Attach a TTL
        // ============================================================

        ConsoleHelper.Section("Expire (set a TTL)");

        await client.ExpireAsync(key, TimeSpan.FromSeconds(60));
        ConsoleHelper.Info($"TTL after Expire(60s) → {Describe(await Ttl(client, key))}");

        // ============================================================
        // Make it permanent again
        // ============================================================

        ConsoleHelper.Section("Persist (clear the TTL)");

        await client.PersistAsync(key);
        ConsoleHelper.Info($"TTL after Persist → {Describe(await Ttl(client, key))}");

        // ============================================================
        // A short TTL that actually elapses
        // ============================================================

        ConsoleHelper.Section("Watch a key expire");

        await client.ExpireAsync(key, TimeSpan.FromSeconds(1));
        ConsoleHelper.Info("Set TTL to 1s. Waiting 1.5s...");
        await Task.Delay(TimeSpan.FromMilliseconds(1500));

        var afterExpiry = await client.GetAsync<string>(key);
        if (afterExpiry.IsSuccess)
            ConsoleHelper.Error($"Key still present (unexpected): {afterExpiry.Value}");
        else
            ConsoleHelper.Success($"Key is gone after its TTL → Status: {afterExpiry.Status}");

        // ============================================================
        // Cleanup
        // ============================================================

        ConsoleHelper.Section("Cleanup");

        await client.ClearAsync(new ClearRequest(context.Prefix));

        ConsoleHelper.Success("Cleanup completed.");
    }

    private static async Task<TimeSpan?> Ttl(IZarisClient client, string key)
    {
        var result = await client.GetTimeToLiveAsync(key);
        return result.IsSuccess ? result.Value : null;
    }

    private static string Describe(TimeSpan? ttl)
        => ttl.HasValue ? $"{ttl.Value.TotalSeconds:F0}s remaining" : "none (persistent)";
}
