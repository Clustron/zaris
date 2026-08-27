using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Clustron.Zaris.Abstractions;
using Clustron.Zaris.Client;
using Clustron.Zaris.Samples.Shared;

namespace Clustron.Zaris.Sample.Batch;

/// <summary>
/// Demonstrates the mixed <c>ExecuteBatchAsync</c> API: a single request carrying a mix of
/// PUT / GET / DELETE operations, dispatched in one round-trip per owning node. This is distinct
/// from the Bulk sample (PutMany/GetMany/DeleteMany), which handles one operation kind at a time.
/// </summary>
internal class BatchSampleApp
{
    private readonly IZarisClientProvider _provider;

    private const string StoreName = "teststore";

    public BatchSampleApp(IZarisClientProvider provider)
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

        var context = new SampleContext("batch");

        ConsoleHelper.Info($"Session Prefix: {context.Prefix}");

        var a = context.Key("fruit:a");
        var b = context.Key("fruit:b");
        var c = context.Key("fruit:c");

        // ============================================================
        // A write batch: three PUTs in one request
        // ============================================================

        ConsoleHelper.Section("Write batch (3 PUTs)");

        var writes = new KvBatchRequest
        {
            Items = new List<KvBatchItem>
            {
                new() { Index = 0, Op = KvBatchOp.Put, Key = a, PutValue = "apple" },
                new() { Index = 1, Op = KvBatchOp.Put, Key = b, PutValue = "banana" },
                new() { Index = 2, Op = KvBatchOp.Put, Key = c, PutValue = "cherry" },
            }
        };

        var writeResponse = await client.ExecuteBatchAsync<string>(writes);
        PrintResults(writeResponse);

        // ============================================================
        // A mixed batch: GET, DELETE, then GET the deleted key
        // ============================================================

        ConsoleHelper.Section("Mixed batch (GET + DELETE + GET)");

        var mixed = new KvBatchRequest
        {
            Items = new List<KvBatchItem>
            {
                new() { Index = 0, Op = KvBatchOp.Get, Key = a },      // hit
                new() { Index = 1, Op = KvBatchOp.Delete, Key = b },   // remove banana
                new() { Index = 2, Op = KvBatchOp.Get, Key = b },      // now a miss
                new() { Index = 3, Op = KvBatchOp.Get, Key = c },      // hit
            }
        };

        var mixedResponse = await client.ExecuteBatchAsync<string>(mixed);
        PrintResults(mixedResponse);

        // ============================================================
        // Cleanup
        // ============================================================

        ConsoleHelper.Section("Cleanup");

        await client.ClearAsync(new ClearRequest(context.Prefix));

        ConsoleHelper.Success("Cleanup completed.");
    }

    private static void PrintResults(KvBatchResponse response)
    {
        foreach (var r in response.Results.OrderBy(x => x.Index))
        {
            var value = r.ValueObject is null ? "" : $" value={r.ValueObject}";
            Console.WriteLine($"  [{r.Index}] {r.Op,-6} success={r.Success} status={r.Status}{value}");
        }
    }
}
