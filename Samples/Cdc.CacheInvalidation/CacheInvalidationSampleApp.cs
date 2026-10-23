using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Clustron.Zaris.Abstractions;
using Clustron.Zaris.Abstractions.ChangeFeed;
using Clustron.Zaris.Client;
using Clustron.Zaris.Client.ChangeFeed;
using Clustron.Zaris.Samples.Shared;

namespace Clustron.Zaris.Sample.Cdc.CacheInvalidation;

/// <summary>
/// A secondary Change Data Capture (CDC) sample: keep a local in-memory cache coherent with the
/// store by consuming the change feed — refresh on <c>Put</c>, evict on <c>Delete</c>. Because the
/// feed is ordered per key and delivered at-least-once, the upsert/evict apply is naturally
/// idempotent, so a re-delivered event does no harm.
/// </summary>
internal sealed class CacheInvalidationSampleApp
{
    private readonly IZarisClientProvider _provider;

    private const string StoreName = "teststore";

    public CacheInvalidationSampleApp(IZarisClientProvider provider)
    {
        _provider = provider;
    }

    public async Task RunAsync()
    {
        var client = await _provider.GetAsync(StoreName);
        ConsoleHelper.Success($"Connected to store: {StoreName}");

        // The change feed needs the server op-log and the remote client; the embedded (InProc)
        // engine does not implement it. This is the ONE way the CDC samples differ from the others
        // (which default to InProc): they default to a Remote connection string — see the README.
        if (client is not IZarisChangeFeedClient feed)
        {
            ConsoleHelper.Error(
                "This sample needs a running cluster with the change feed enabled (ZARIS_WAN_SYNC_FEED=1). " +
                "Point the 'teststore' connection string in appsettings.json at your cluster " +
                "(zaris://host:port/teststore) — the in-process engine cannot serve a change feed.");
            return;
        }

        // Per-run key isolation (same as the other samples), so repeated runs never collide.
        var context = new SampleContext("cdc-cache");
        ConsoleHelper.Info($"Session Prefix: {context.Prefix}");

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        // The local cache we keep coherent with the store. In a real app this is your HTTP response
        // cache, an in-memory lookup, a near-cache in front of the store, etc.
        var cache = new Dictionary<string, string>();

        // ---- 1) make some changes in the store ----------------------------------------------------
        ConsoleHelper.Section("Mutating the store");
        await client.PutAsync(context.Key("product:1"), "Keyboard");
        await client.PutAsync(context.Key("product:2"), "Mouse");
        await client.PutAsync(context.Key("product:3"), "Monitor");
        await client.PutAsync(context.Key("product:2"), "Mouse (wireless)");   // update
        await client.DeleteAsync(context.Key("product:3"));                     // delete
        ConsoleHelper.Info("Wrote 5 mutations (3 puts, 1 update, 1 delete).");
        const int expectedEvents = 5;

        // ---- 2) rebuild/maintain the cache from the change feed -----------------------------------
        ConsoleHelper.Section("Applying the change feed to the local cache");

        var seen = new HashSet<string>();   // dedup: at-least-once can re-deliver
        int applied = 0;

        var options = new ChangeFeedOptions
        {
            KeyPrefix    = context.Prefix,                       // only this run's keys (server-side)
            ConsumerName = $"cache-rebuilder-{context.SessionId}", // fresh name → replays this run from the start
            Kinds        = new[] { FeedEventKind.Put, FeedEventKind.Delete },
        };

        var consume = Task.Run(async () =>
        {
            try
            {
                await foreach (var ev in feed.OpenChangeFeed(options, cts.Token).ConfigureAwait(false))
                {
                    if (!seen.Add($"{ev.Key}|{ev.Kind}|{ev.Revision}")) continue;

                    if (ev.IsDelete)
                    {
                        cache.Remove(ev.Key);
                        Console.WriteLine($"[CACHE] EVICT   {ev.Key}  (size={cache.Count})");
                    }
                    else
                    {
                        // ev.Value is the raw byte[] of a plain-value write; decode for display.
                        cache[ev.Key] = ev.Value is { } bytes ? Encoding.UTF8.GetString(bytes) : "<non-plain>";
                        Console.WriteLine($"[CACHE] REFRESH {ev.Key} = {cache[ev.Key]}  (size={cache.Count})");
                    }

                    if (++applied >= expectedEvents) cts.CancelAfter(TimeSpan.FromMilliseconds(750));
                }
            }
            catch (OperationCanceledException) { /* stopped: grace elapsed or Ctrl+C */ }
        }, cts.Token);

        await Task.WhenAny(consume, Task.Delay(TimeSpan.FromSeconds(30))).ConfigureAwait(false);
        if (!cts.IsCancellationRequested) cts.Cancel();
        try { await consume.ConfigureAwait(false); } catch { }

        // ---- 3) show the resulting cache ----------------------------------------------------------
        ConsoleHelper.Section("Final cache (coherent with the store)");
        foreach (var kv in cache)
            Console.WriteLine($"  {kv.Key} = {kv.Value}");
        ConsoleHelper.Success($"Cache holds {cache.Count} entries (product:3 was evicted by its Delete).");

        // ---- cleanup ------------------------------------------------------------------------------
        ConsoleHelper.Section("Cleanup");
        await client.ClearAsync(new ClearRequest(context.Prefix));
        ConsoleHelper.Success("Cleanup completed.");
    }
}
