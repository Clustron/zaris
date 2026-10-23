// Clustron Zaris – Change Data Capture (CDC) sample: cache invalidation.
//
// Consume the Zaris change feed and keep a local in-process cache coherent with the store:
//   - a Put   refreshes the cached bytes (or, for a collection/counter, drops the stale entry),
//   - a Delete removes the entry.
// Delivery is at-least-once and per-key ordered, so upsert-on-Put / remove-on-Delete is idempotent.
//
//   dotnet run -- "zaris://localhost:7861/mystore" "catalog/"
//
// The second arg is an optional key prefix (server-side filtered). Runs until Ctrl+C.
// Requires a running cluster with the change feed enabled (changeFeed.enabled=true or
// ZARIS_WAN_SYNC_FEED=1), and the Zaris 2.1.0+ SDK.

using System.Collections.Concurrent;
using Clustron.Zaris.Abstractions.ChangeFeed;
using Clustron.Zaris.Client;
using Clustron.Zaris.Client.ChangeFeed;
using Clustron.Zaris.Client.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

var connectionString = args.Length > 0
    ? args[0]
    : Environment.GetEnvironmentVariable("ZARIS_CONNECTION_STRING")
      ?? throw new ArgumentException("Pass a connection string as arg[0] or set ZARIS_CONNECTION_STRING.");

var keyPrefix = args.Length > 1 ? args[1] : null;

// The registration key is just a LOCAL DI handle; the cluster identity comes from the connection string path.
const string key = "app";
var services = new ServiceCollection();
services.AddClustronZaris(key, connectionString);
using var provider = services.BuildServiceProvider();

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

var client = await provider.GetRequiredService<IZarisClientProvider>().GetAsync(key, cts.Token);
var feed = (IZarisChangeFeedClient)client; // ZarisClusterClient implements IZarisChangeFeedClient

// The local cache we keep coherent. In a real app this is your HTTP response cache, a Redis-side cache, etc.
var cache = new ConcurrentDictionary<string, byte[]>();

var options = new ChangeFeedOptions
{
    KeyPrefix    = keyPrefix,          // null = all keys; otherwise server-side filtered
    ConsumerName = "catalog-cache",    // durable name → server-tracked lag + best-effort resume
};

Console.WriteLine($"[cdc-cache] watching feed (prefix='{keyPrefix ?? "*"}') — Ctrl+C to stop");

try
{
    await foreach (ChangeEvent ev in feed.OpenChangeFeed(options, cts.Token))
    {
        if (ev.IsDelete)
        {
            cache.TryRemove(ev.Key, out _);
            Console.WriteLine($"[cdc-cache] DEL  {ev.Key}  (cache size {cache.Count})");
        }
        else if (ev.Value is { } bytes) // a plain-value write
        {
            cache[ev.Key] = bytes;      // refresh in place (swap for TryRemove to invalidate-only)
            Console.WriteLine($"[cdc-cache] PUT  {ev.Key}  {bytes.Length}B  (cache size {cache.Count})");
        }
        else
        {
            // Collection / counter change — ev.Value is null; drop the stale entry and let it be re-read lazily.
            cache.TryRemove(ev.Key, out _);
            Console.WriteLine($"[cdc-cache] INVD {ev.Key}  (non-plain value; dropped)");
        }
    }
}
catch (OperationCanceledException) { /* Ctrl+C */ }

Console.WriteLine($"[cdc-cache] stopped. final cache size {cache.Count}");
return 0;
